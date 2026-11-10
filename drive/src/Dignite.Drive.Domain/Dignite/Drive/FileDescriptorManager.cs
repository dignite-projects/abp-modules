using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Services;

namespace Dignite.Drive;

/// <summary>
/// Stores files in Drive containers and removes them. The upload rules - size cap, allowed content types, image
/// processing - are the container's BLOB pipeline contributors, which run inside <c>IBlobContainer.SaveAsync</c>;
/// this class is the caller around them: it buffers and detects the upload, hashes it, names the physical BLOB,
/// writes the descriptor, dedups by content and keeps BLOB and descriptor from drifting apart.
/// <para>
/// It authorizes nobody: whoever calls it has been authorized by an application service, or is trusted in-process.
/// </para>
/// </summary>
public class FileDescriptorManager : DomainService
{
    /// <summary>
    /// Compensation runs on its own bounded token: when the request was cancelled, reusing the request token would
    /// cancel the cleanup at once and leave the BLOB behind.
    /// </summary>
    protected static readonly TimeSpan CompensationTimeout = TimeSpan.FromSeconds(30);

    protected IFileDescriptorRepository FileDescriptorRepository { get; }

    protected IBlobContainerFactory BlobContainerFactory { get; }

    protected IBlobContainerConfigurationProvider BlobContainerConfigurationProvider { get; }

    protected IMimeTypeDetector MimeTypeDetector { get; }

    protected DriveUsageManager UsageManager { get; }

    public FileDescriptorManager(
        IFileDescriptorRepository fileDescriptorRepository,
        IBlobContainerFactory blobContainerFactory,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider,
        IMimeTypeDetector mimeTypeDetector,
        DriveUsageManager usageManager)
    {
        FileDescriptorRepository = fileDescriptorRepository;
        BlobContainerFactory = blobContainerFactory;
        BlobContainerConfigurationProvider = blobContainerConfigurationProvider;
        MimeTypeDetector = mimeTypeDetector;
        UsageManager = usageManager;
    }

    /// <summary>
    /// Stores <paramref name="stream"/> in <paramref name="containerName"/> under <paramref name="name"/> and records it.
    /// <para>
    /// When a live descriptor already has <paramref name="name"/>, the call fails with
    /// <see cref="BlobAlreadyExistsException"/> unless <paramref name="overrideExisting"/> is set; then the new
    /// descriptor replaces it once it is stored, and the old one is deleted as <see cref="DeleteAsync"/> deletes.
    /// </para>
    /// <para>
    /// Sequence: the upload is copied once into memory, capped at what the container's pipeline enforces (its
    /// <c>MaxSize</c> contributor, else <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/>), so an
    /// oversized upload fails with <c>Dignite.Abp.BlobStoring:ContentTooLarge</c> before anything is stored. Its type
    /// is detected from the bytes and reconciled with the extension of <paramref name="name"/>
    /// (<c>Dignite.Abp.BlobStoring:ContentTypeMismatch</c>), and it is hashed (SHA-256, upper-case hex).
    /// </para>
    /// <para>
    /// The tenant's allocation is checked first: a tenant whose Drive is off is refused
    /// (<see cref="DriveErrorCodes.DriveDisabled"/>), and content that has to be stored - not a dedup hit, which adds
    /// no bytes - is refused when it would take the tenant past its storage size
    /// (<see cref="DriveErrorCodes.StorageQuotaExceeded"/>, measured by the upload's length, see
    /// <see cref="DriveUsageManager"/>). A replaced descriptor's bytes still count while the check runs.
    /// </para>
    /// <para>
    /// Dedup comes before any write: when a live descriptor in the same container (and tenant) has the same hash, the
    /// new descriptor shares its BLOB and nothing is stored. A dedup hit skips the container's contributors - the
    /// bytes passed them when they were stored.
    /// </para>
    /// <para>
    /// Otherwise the BLOB is saved under a new name (<see cref="CreateBlobName"/>) without overwriting - the
    /// contributors run here and may reject the upload - and the descriptor is written with <c>autoSave</c>, holding
    /// the dedup key; a failed write deletes the BLOB again. The lookup is check-then-act, so two simultaneous uploads
    /// of the same bytes can both miss it: the unique dedup key is the arbiter - the loser's insert fails, its BLOB is
    /// deleted, and a retry finds the winner and shares its BLOB.
    /// </para>
    /// </summary>
    /// <param name="name">The caller's name for the file: in a system container, the BLOB name it reads it back by.
    /// Its extension, if any, is checked against the content.</param>
    /// <param name="clientId">The OAuth client writing through the BLOB API, recorded on the descriptor.</param>
    public virtual async Task<FileDescriptor> CreateAsync(
        string containerName,
        string name,
        Stream stream,
        bool overrideExisting = false,
        string? clientId = null,
        CancellationToken cancellationToken = default)
    {
        GetDriveContainerConfiguration(containerName);
        Check.NotNullOrWhiteSpace(name, nameof(name), FileDescriptorConsts.MaxNameLength);
        Check.NotNull(stream, nameof(stream));
        await UsageManager.CheckEnabledAsync();

        var existing = await FileDescriptorRepository.FindByNameAsync(containerName, name, cancellationToken);
        if (existing != null && !overrideExisting)
        {
            throw new BlobAlreadyExistsException(
                $"The Drive container '{containerName}' already has a file named '{name}'. Set overrideExisting if it should be overwritten.");
        }

        var maxBytes = BlobStreamBuffering.GetMaxBufferedBytes(BlobContainerConfigurationProvider.Get(containerName));
        await using var content = await BlobStreamBuffering.CopyToBufferAsync(stream, maxBytes, cancellationToken);

        var mimeType = await MimeTypeDetector.DetectAsync(content, name, cancellationToken);
        var hash = await ComputeHashAsync(content, cancellationToken);

        FileDescriptor descriptor;
        var source = await FindDedupSourceAsync(containerName, hash, cancellationToken);
        if (source != null)
        {
            // The bytes already have a home: nothing is stored, the new row shares it.
            descriptor = await FileDescriptorRepository.InsertAsync(
                new FileDescriptor(
                    GuidGenerator.Create(), containerName, name, source.BlobName, source.MimeType, source.Size, hash,
                    ownsBlob: false, directoryId: null, clientId, CurrentTenant.Id),
                autoSave: true,
                cancellationToken);
        }
        else
        {
            descriptor = await StoreAsync(containerName, name, content, mimeType, hash, clientId, cancellationToken);
        }

        if (existing != null)
        {
            await DeleteAsync(existing, cancellationToken);
        }

        return descriptor;
    }

    /// <summary>
    /// Deletes the descriptor (soft), then its BLOB unless another live descriptor still names it.
    /// <para>
    /// The row is deleted with <c>autoSave</c> before the check runs, so this descriptor does not count itself. A crash
    /// between the two steps leaves an unreferenced BLOB - unreachable bytes, never lost data: ABP's BLOB containers
    /// cannot be enumerated, so reclaiming such orphans belongs to tooling on the concrete storage backend.
    /// </para>
    /// </summary>
    public virtual async Task DeleteAsync(FileDescriptor file, CancellationToken cancellationToken = default)
    {
        if (file.OwnsBlob())
        {
            // Saved on its own: ABP's EF Core soft delete reloads the row before it marks it deleted, which would
            // discard a change made in the same save.
            file.ReleaseDedupKey();
            await FileDescriptorRepository.UpdateAsync(file, autoSave: true, cancellationToken);
        }

        await FileDescriptorRepository.DeleteAsync(file, autoSave: true, cancellationToken);

        if (await FileDescriptorRepository.IsBlobInUseAsync(file.ContainerName, file.BlobName, cancellationToken))
        {
            return;
        }

        await BlobContainerFactory.Create(file.ContainerName).DeleteAsync(file.BlobName, cancellationToken);
    }

    /// <summary>The live descriptor named <paramref name="name"/> in <paramref name="containerName"/>, or <c>null</c>.</summary>
    public virtual async Task<FileDescriptor?> FindAsync(
        string containerName,
        string name,
        CancellationToken cancellationToken = default)
    {
        GetDriveContainerConfiguration(containerName);
        if (name.IsNullOrWhiteSpace())
        {
            return null;
        }

        return await FileDescriptorRepository.FindByNameAsync(containerName, name, cancellationToken);
    }

    /// <summary>The bytes of <paramref name="file"/>, or <c>null</c> when its BLOB is missing from storage.</summary>
    public virtual Task<Stream?> GetStreamOrNullAsync(FileDescriptor file, CancellationToken cancellationToken = default)
    {
        return BlobContainerFactory.Create(file.ContainerName).GetOrNullAsync(file.BlobName, cancellationToken);
    }

    /// <summary>
    /// The Drive configuration of <paramref name="containerName"/>; fails with
    /// <see cref="DriveErrorCodes.ContainerNotFound"/> for a container the Drive host did not declare.
    /// </summary>
    public virtual DriveContainerConfiguration GetDriveContainerConfiguration(string containerName)
    {
        Check.NotNullOrWhiteSpace(containerName, nameof(containerName));

        var configuration = BlobContainerConfigurationProvider.Get(containerName).GetDriveContainerConfiguration();
        if (!configuration.IsDriveContainer)
        {
            throw new BusinessException(DriveErrorCodes.ContainerNotFound)
                .WithData("ContainerName", containerName);
        }

        return configuration;
    }

    /// <summary>A live descriptor whose bytes an upload hashing to <paramref name="hash"/> can share.</summary>
    protected virtual Task<FileDescriptor?> FindDedupSourceAsync(
        string containerName,
        string hash,
        CancellationToken cancellationToken)
    {
        return FileDescriptorRepository.FindByHashAsync(containerName, hash, cancellationToken);
    }

    /// <summary>Saves the content under a new BLOB name and writes the descriptor that owns it.</summary>
    protected virtual async Task<FileDescriptor> StoreAsync(
        string containerName,
        string name,
        Stream content,
        string mimeType,
        string hash,
        string? clientId,
        CancellationToken cancellationToken)
    {
        // Taken before the save: the content belongs to the pipeline while it runs.
        var uploadLength = content.Length;
        await UsageManager.CheckCanStoreAsync(uploadLength, cancellationToken);

        var blobName = CreateBlobName(mimeType);
        var container = BlobContainerFactory.Create(containerName);
        try
        {
            await container.SaveAsync(blobName, content, overrideExisting: false, cancellationToken);
        }
        catch (Exception exception) when (exception is not BlobAlreadyExistsException and not BusinessException)
        {
            // The provider failed after it may have written bytes. A rejection by a contributor (a BusinessException)
            // happens before the provider writes anything, and a name collision means the BLOB belongs to another
            // descriptor - neither is compensated.
            await TryDeleteBlobAsync(container, blobName);
            throw;
        }

        try
        {
            var (size, storedType) = await GetStoredSizeAndTypeAsync(container, blobName, mimeType, uploadLength, cancellationToken);

            return await FileDescriptorRepository.InsertAsync(
                new FileDescriptor(
                    GuidGenerator.Create(), containerName, name, blobName, storedType, size, hash,
                    ownsBlob: true, directoryId: null, clientId, CurrentTenant.Id),
                autoSave: true,
                cancellationToken);
        }
        catch
        {
            await TryDeleteBlobAsync(container, blobName);
            throw;
        }
    }

    /// <summary>
    /// A new physical BLOB name: 32 hexadecimal digits of a random GUID plus the canonical extension of
    /// <paramref name="mimeType"/> from <see cref="DriveFileTypes"/> (none when the table has no row for it).
    /// <see cref="Guid.NewGuid"/> rather than the sequential <c>IGuidGenerator</c>: the name may become a public
    /// address, so it stays fully random. The extension follows the <b>detected</b> type, so the contributors, which
    /// reconcile the content with the BLOB name's extension inside <c>SaveAsync</c>, agree with the content.
    /// </summary>
    protected virtual string CreateBlobName(string mimeType)
    {
        return Guid.NewGuid().ToString("N") + (DriveFileTypes.GetExtension(mimeType) ?? string.Empty);
    }

    /// <summary>SHA-256 of the content as upper-case hex; the stream is rewound afterwards.</summary>
    protected virtual async Task<string> ComputeHashAsync(Stream content, CancellationToken cancellationToken)
    {
        content.Position = 0;
        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        content.Position = 0;
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// The size and type of what was stored under <paramref name="blobName"/>. Image contributors only ever touch
    /// <c>image/*</c> content, so for anything else they are the upload's. An image may have been resized, re-encoded
    /// or converted, so it is read back and its type detected from the stored bytes: the row records what is served.
    /// </summary>
    protected virtual async Task<(long Size, string MimeType)> GetStoredSizeAndTypeAsync(
        IBlobContainer container,
        string blobName,
        string expectedMimeType,
        long uploadLength,
        CancellationToken cancellationToken)
    {
        if (!expectedMimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return (uploadLength, expectedMimeType);
        }

        await using var stored = await container.GetAsync(blobName, cancellationToken);
        using var copy = new MemoryStream();
        await stored.CopyToAsync(copy, cancellationToken);
        copy.Position = 0;
        var mimeType = await MimeTypeDetector.DetectAsync(copy, blobName, cancellationToken);
        return (copy.Length, mimeType);
    }

    protected virtual async Task TryDeleteBlobAsync(IBlobContainer container, string blobName)
    {
        using var compensation = new CancellationTokenSource(CompensationTimeout);
        try
        {
            await container.DeleteAsync(blobName, compensation.Token);
        }
        catch
        {
            // Best-effort compensation must not hide the original exception.
        }
    }
}
