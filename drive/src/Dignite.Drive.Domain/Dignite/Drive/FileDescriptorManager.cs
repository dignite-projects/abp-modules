using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Services;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Dignite.Drive;

/// <summary>
/// Stores files in Drive containers and removes them. The upload rules - size cap, allowed content types, image
/// processing - are the container's BLOB pipeline contributors, which run inside <c>IBlobContainer.SaveAsync</c>;
/// this class is the caller around them: it buffers and detects the upload, hashes it, names the physical BLOB,
/// writes the descriptor, dedups by content and keeps BLOB and descriptor from drifting apart.
/// <para>
/// It authorizes nobody: whoever calls it has been authorized by an application service, or is trusted in-process.
/// </para>
/// <para>
/// The two entry points meet here and stay apart: the name-addressed operations (<see cref="CreateAsync"/>,
/// <see cref="FindAsync"/>) serve system containers only, and the user-drive operation (<see cref="UploadAsync"/>) user
/// drives only; each treats a container of the other kind as one that does not exist.
/// </para>
/// <para>
/// <b>Consistency.</b> A physical BLOB is deleted only after the unit of work that deleted its last descriptor has
/// committed (<see cref="DeleteAsync"/>), so a rollback never brings back a row whose bytes are gone. Uploads and those
/// deletions of the same bytes take turns on the <b>dedup lock</b> (<see cref="IAbpDistributedLock"/>, named by the
/// dedup key), which an upload holds until its unit of work is over: a deletion that waited for it sees the
/// uploader's row, or its absence, and never removes bytes a new row is about to share. ABP's default lock is
/// in-process; several Drive instances need a distributed lock provider - the same requirement as the tickets.
/// </para>
/// </summary>
public class FileDescriptorManager : DomainService
{
    /// <summary>
    /// Compensation runs on its own bounded token: when the request was cancelled, reusing the request token would
    /// cancel the cleanup at once and leave the BLOB behind.
    /// </summary>
    protected static readonly TimeSpan CompensationTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long an upload, or the deletion of a BLOB after commit, waits for the dedup lock. Bounded so a stuck holder
    /// costs a missed dedup or an orphaned BLOB, never a hung request.
    /// </summary>
    protected static readonly TimeSpan DedupLockTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The dedup lock's name is this prefix and <see cref="FileDescriptor.CreateDedupKey"/>.</summary>
    protected const string DedupLockName = "Dignite.Drive.Dedup:";

    /// <summary>
    /// The <see cref="IUnitOfWork.Items"/> entry with the dedup locks a unit of work holds, by lock name: a unit of
    /// work that needs one of them again reuses it, since the lock is not reentrant and would otherwise wait for itself.
    /// </summary>
    protected const string DedupLocksItemKey = "Dignite.Drive.FileDescriptorManager.DedupLocks";

    protected IFileDescriptorRepository FileDescriptorRepository { get; }

    protected IBlobContainerFactory BlobContainerFactory { get; }

    protected IBlobContainerConfigurationProvider BlobContainerConfigurationProvider { get; }

    protected IMimeTypeDetector MimeTypeDetector { get; }

    protected DriveUsageManager UsageManager { get; }

    // Resolved lazily, not injected, so the constructor - which subclasses repeat - stays as it was.
    protected IAbpDistributedLock DistributedLock => LazyServiceProvider.LazyGetRequiredService<IAbpDistributedLock>();

    protected IUnitOfWorkManager UnitOfWorkManager => LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>();

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
    /// Stores <paramref name="stream"/> in the system container <paramref name="containerName"/> under
    /// <paramref name="name"/> and records it.
    /// <para>
    /// When a live descriptor already has <paramref name="name"/>, the call fails with
    /// <see cref="DriveErrorCodes.BlobAlreadyExists"/> unless <paramref name="overrideExisting"/> is set; then the new
    /// descriptor replaces it: the old one gives up its <see cref="FileDescriptor.NameKey"/> (saved at once - the
    /// key is unique, so both cannot hold it), the new one is stored, and the old one is deleted as
    /// <see cref="DeleteAsync"/> deletes. If the new one cannot be stored, the old one takes its key back.
    /// </para>
    /// <para>
    /// The lookup by name is check-then-act: of two simultaneous saves under one name, the unique name key lets only
    /// one insert succeed; the loser fails with the database provider's exception and its BLOB is deleted again.
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
    /// Dedup comes before any write and under the dedup lock (see <see cref="StoreOrShareAsync"/>): when a live
    /// descriptor in the same container (and tenant) has the same hash, the new descriptor shares its BLOB and nothing
    /// is stored. A dedup hit skips the container's contributors - the bytes passed them when they were stored.
    /// </para>
    /// <para>
    /// Otherwise the BLOB is saved under a new name (<see cref="CreateBlobName"/>) without overwriting - the
    /// contributors run here and may reject the upload - and the descriptor is written with <c>autoSave</c>, holding
    /// the dedup key; a failed write deletes the BLOB again. The lock makes two simultaneous uploads of the same bytes
    /// take turns, so the second shares the first one's BLOB; the unique dedup key stays the last-resort arbiter - a
    /// loser's insert fails, its BLOB is deleted, and a retry finds the winner and shares its BLOB.
    /// </para>
    /// <para>
    /// A container may store content in another format than it was uploaded in - the images preset stores a JPEG as WebP.
    /// The physical BLOB is named for the stored type, which the container declares (<see cref="GetStoredMimeType"/>), and
    /// the descriptor records the type and size of the stored bytes; <paramref name="name"/>, the application's address,
    /// is kept. Dedup still compares the upload's hash, so the same upload shares the stored result.
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
        GetDriveContainerConfiguration(containerName, DriveContainerKind.System);
        Check.NotNullOrWhiteSpace(name, nameof(name), FileDescriptorConsts.MaxNameLength);
        Check.NotNull(stream, nameof(stream));
        await UsageManager.CheckEnabledAsync();

        var existing = await FileDescriptorRepository.FindByNameAsync(containerName, name, cancellationToken);
        if (existing != null && !overrideExisting)
        {
            throw new BusinessException(DriveErrorCodes.BlobAlreadyExists)
                .WithData("ContainerName", containerName)
                .WithData("BlobName", name);
        }

        // The replaced descriptor gives up the address before the new one is inserted: the name key is unique, so
        // both cannot hold it at once. (A row written before the key existed has none to give up.)
        var releasedNameKey = false;
        if (existing?.NameKey != null)
        {
            existing.ReleaseNameKey();
            await FileDescriptorRepository.UpdateAsync(existing, autoSave: true, cancellationToken);
            releasedNameKey = true;
        }

        FileDescriptor descriptor;
        try
        {
            descriptor = await StoreOrShareAsync(
                containerName, name, stream, clientId, nameIsAddress: true, configureFile: null, cancellationToken);
        }
        catch
        {
            if (releasedNameKey)
            {
                await TryClaimNameKeyAsync(existing!);
            }

            throw;
        }

        if (existing != null)
        {
            await DeleteAsync(existing, cancellationToken);
        }

        return descriptor;
    }

    /// <summary>
    /// Stores <paramref name="stream"/> as a new file named <paramref name="name"/> in the user drive
    /// <paramref name="containerName"/>. The name need not be unique: a user-drive file is addressed by its id, and holds
    /// no <see cref="FileDescriptor.NameKey"/>. Otherwise it is stored as <see cref="CreateAsync"/> stores - allocation,
    /// buffering, detection, dedup under the dedup lock, compensation. One difference: when the container stores the
    /// upload in another format (a JPEG as WebP), the name takes that format's extension
    /// (<see cref="GetNameForStoredType"/>), shared content included.
    /// </summary>
    /// <param name="configureFile">Runs on the new descriptor before it is inserted, whether its bytes are stored or
    /// shared with a descriptor that has them already: a module that keeps fields of its own on the file (ABP object
    /// extensions) sets them here, so the row is written once - atomic on every database provider, with or without a
    /// transaction. It runs while the dedup lock is held, so it does no I/O; when it throws, the upload fails and nothing
    /// is stored.</param>
    public virtual async Task<FileDescriptor> UploadAsync(
        string containerName,
        string name,
        Stream stream,
        Action<FileDescriptor>? configureFile = null,
        CancellationToken cancellationToken = default)
    {
        GetDriveContainerConfiguration(containerName, DriveContainerKind.UserDrive);
        Check.NotNullOrWhiteSpace(name, nameof(name), FileDescriptorConsts.MaxNameLength);
        Check.NotNull(stream, nameof(stream));
        await UsageManager.CheckEnabledAsync();

        return await StoreOrShareAsync(
            containerName, name, stream, clientId: null, nameIsAddress: false, configureFile, cancellationToken);
    }

    /// <summary>
    /// Buffers, detects and hashes the upload, then shares the BLOB of a live descriptor with the same content or stores
    /// it (<see cref="StoreAsync"/>), and writes the new descriptor.
    /// <para>
    /// The dedup lock for the content is taken before the lookup and held until the current unit of work is over
    /// (released when it is disposed, after commit or rollback; at the end of this method when there is none). So a
    /// deletion of the source's last descriptor, which takes the same lock before it removes the BLOB, either ran
    /// before the lookup - which then misses - or runs after this row is committed and sees it. A unit of work that
    /// already holds the lock reuses it.
    /// </para>
    /// <para>
    /// When the lock cannot be had within <see cref="DedupLockTimeout"/>, the upload neither fails nor dedups: the
    /// content is stored as a fresh BLOB that its descriptor does not claim the dedup key for - its name is new, so
    /// nothing else references it, and it cannot collide with the key's live holder.
    /// </para>
    /// </summary>
    /// <param name="nameIsAddress">Whether the container is a system container, whose descriptors hold the
    /// <see cref="FileDescriptor.NameKey"/>.</param>
    /// <param name="configureFile">Runs on the new descriptor before it is inserted (see <see cref="UploadAsync"/>).</param>
    protected virtual async Task<FileDescriptor> StoreOrShareAsync(
        string containerName,
        string name,
        Stream stream,
        string? clientId,
        bool nameIsAddress,
        Action<FileDescriptor>? configureFile,
        CancellationToken cancellationToken)
    {
        var maxBytes = BlobStreamBuffering.GetMaxBufferedBytes(BlobContainerConfigurationProvider.Get(containerName));
        await using var content = await BlobStreamBuffering.CopyToBufferAsync(stream, maxBytes, cancellationToken);

        var mimeType = await MimeTypeDetector.DetectAsync(content, name, cancellationToken);
        var hash = await ComputeHashAsync(content, cancellationToken);

        var lockName = GetDedupLockName(CurrentTenant.Id, containerName, hash);
        var (locked, releaseOnReturn) = await AcquireDedupLockAsync(lockName, cancellationToken);
        try
        {
            if (!locked)
            {
                Logger.LogWarning(
                    "The Drive could not take the dedup lock {LockName} within {Timeout}; the upload to {ContainerName} is stored without dedup.",
                    lockName, DedupLockTimeout, containerName);

                return await StoreAsync(
                    containerName, name, content, mimeType, hash, clientId, ownsBlob: false, nameIsAddress,
                    configureFile, cancellationToken);
            }

            var source = await FindDedupSourceAsync(containerName, hash, cancellationToken);
            if (source != null)
            {
                // The bytes already have a home: nothing is stored, the new row shares it - and its type, which a
                // user-drive name follows.
                var sharedName = nameIsAddress ? name : GetNameForStoredType(name, mimeType, source.MimeType);
                var shared = new FileDescriptor(
                    GuidGenerator.Create(), containerName, sharedName, source.BlobName, source.MimeType, source.Size, hash,
                    ownsBlob: false, clientId, CurrentTenant.Id, nameIsAddress);
                configureFile?.Invoke(shared);
                return await FileDescriptorRepository.InsertAsync(shared, autoSave: true, cancellationToken);
            }

            return await StoreAsync(
                containerName, name, content, mimeType, hash, clientId, ownsBlob: true, nameIsAddress,
                configureFile, cancellationToken);
        }
        finally
        {
            if (releaseOnReturn != null)
            {
                await releaseOnReturn.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Deletes the descriptor (soft) in the current unit of work, then - once that unit of work has committed - its
    /// BLOB, unless another live descriptor still names it.
    /// <para>
    /// The descriptor first gives up its dedup and name keys, so the deleted row keeps neither the same bytes nor the
    /// same name from being stored again. The BLOB step is registered with <c>IUnitOfWork.OnCompleted</c>, which runs
    /// only after a successful commit: a rollback brings the row back with its bytes. Without a unit of work it runs
    /// at once.
    /// </para>
    /// <para>
    /// That step takes the dedup lock for the content (unless the completing unit of work holds it already, as when an
    /// override stored identical content), then asks, in a unit of work of its own, whether a live descriptor still
    /// names the BLOB. An uploader that found this descriptor as its dedup source holds the lock until its own row is
    /// committed or rolled back, so the check sees that row or its absence - never a row about to name bytes that are
    /// gone.
    /// </para>
    /// <para>
    /// The step is best-effort: a failure or a lock timeout is logged and leaves an unreferenced BLOB - unreachable
    /// bytes, never lost data - and does not surface to a request whose transaction already committed. ABP's BLOB
    /// containers cannot be enumerated, so reclaiming such orphans belongs to tooling on the concrete storage backend.
    /// </para>
    /// </summary>
    public virtual async Task DeleteAsync(FileDescriptor file, CancellationToken cancellationToken = default)
    {
        if (file.DedupKey != null || file.NameKey != null)
        {
            // Saved on its own: ABP's EF Core soft delete reloads the row before it marks it deleted, which would
            // discard a change made in the same save.
            file.ReleaseDedupKey();
            file.ReleaseNameKey();
            await FileDescriptorRepository.UpdateAsync(file, autoSave: true, cancellationToken);
        }

        await FileDescriptorRepository.DeleteAsync(file, autoSave: true, cancellationToken);

        // Captured now: the step may run after the caller has moved on from this entity and this tenant.
        var tenantId = file.TenantId;
        var containerName = file.ContainerName;
        var blobName = file.BlobName;
        var hash = file.Hash;

        var unitOfWork = UnitOfWorkManager.Current;
        if (unitOfWork == null)
        {
            await DeleteBlobIfUnusedAsync(tenantId, containerName, blobName, hash, completingUnitOfWork: null);
            return;
        }

        unitOfWork.OnCompleted(() => DeleteBlobIfUnusedAsync(tenantId, containerName, blobName, hash, unitOfWork));
    }

    /// <summary>
    /// The live descriptor named <paramref name="name"/> in the system container <paramref name="containerName"/>, or
    /// <c>null</c>.
    /// </summary>
    public virtual async Task<FileDescriptor?> FindAsync(
        string containerName,
        string name,
        CancellationToken cancellationToken = default)
    {
        GetDriveContainerConfiguration(containerName, DriveContainerKind.System);
        if (name.IsNullOrWhiteSpace())
        {
            return null;
        }

        return await FileDescriptorRepository.FindByNameAsync(containerName, name, cancellationToken);
    }

    /// <summary>
    /// A live file of the user drive <paramref name="containerName"/> whose physical BLOB is <paramref name="blobName"/>,
    /// or <c>null</c>: a user-drive file's public address names its BLOB, since its name need not be unique.
    /// </summary>
    public virtual async Task<FileDescriptor?> FindByBlobNameAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        GetDriveContainerConfiguration(containerName, DriveContainerKind.UserDrive);
        if (blobName.IsNullOrWhiteSpace())
        {
            return null;
        }

        return await FileDescriptorRepository.FindByBlobNameAsync(containerName, blobName, cancellationToken);
    }

    /// <summary>The bytes of <paramref name="file"/>, or <c>null</c> when its BLOB is missing from storage.</summary>
    public virtual Task<Stream?> GetStreamOrNullAsync(FileDescriptor file, CancellationToken cancellationToken = default)
    {
        return BlobContainerFactory.Create(file.ContainerName).GetOrNullAsync(file.BlobName, cancellationToken);
    }

    /// <summary>
    /// The Drive configuration of <paramref name="containerName"/>; fails with
    /// <see cref="DriveErrorCodes.ContainerNotFound"/> for a container the Drive host did not declare, or one of another
    /// <paramref name="kind"/> than asked for.
    /// </summary>
    public virtual DriveContainerConfiguration GetDriveContainerConfiguration(
        string containerName,
        DriveContainerKind? kind = null)
    {
        return BlobContainerConfigurationProvider.GetDriveContainerConfiguration(containerName, kind);
    }

    /// <summary>A live descriptor whose bytes an upload hashing to <paramref name="hash"/> can share.</summary>
    protected virtual Task<FileDescriptor?> FindDedupSourceAsync(
        string containerName,
        string hash,
        CancellationToken cancellationToken)
    {
        return FileDescriptorRepository.FindByHashAsync(containerName, hash, cancellationToken);
    }

    /// <summary>
    /// Saves the content under a new BLOB name and writes the descriptor for it - holding the dedup key when
    /// <paramref name="ownsBlob"/>, which is not the case when the dedup lock could not be taken.
    /// <paramref name="configureFile"/> runs on the descriptor before it is inserted (see <see cref="UploadAsync"/>).
    /// </summary>
    protected virtual async Task<FileDescriptor> StoreAsync(
        string containerName,
        string name,
        Stream content,
        string mimeType,
        string hash,
        string? clientId,
        bool ownsBlob,
        bool nameIsAddress,
        Action<FileDescriptor>? configureFile,
        CancellationToken cancellationToken)
    {
        // Taken before the save: the content belongs to the pipeline while it runs.
        var uploadLength = content.Length;
        await UsageManager.CheckCanStoreAsync(uploadLength, cancellationToken);

        // Named for what the container stores: a JPEG it stores as WebP gets a .webp BLOB.
        var blobName = CreateBlobName(GetStoredMimeType(containerName, mimeType));
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
            var (size, storedType) = await GetStoredSizeAndTypeAsync(
                container, blobName, GetStoredMimeType(containerName, mimeType), uploadLength, cancellationToken);
            var storedName = nameIsAddress ? name : GetNameForStoredType(name, mimeType, storedType);

            var descriptor = new FileDescriptor(
                GuidGenerator.Create(), containerName, storedName, blobName, storedType, size, hash,
                ownsBlob, clientId, CurrentTenant.Id, nameIsAddress);
            configureFile?.Invoke(descriptor);

            // A unique-key violation (the dedup key or the name key) fails here too, and the BLOB goes with it - as it
            // does when configureFile throws.
            return await FileDescriptorRepository.InsertAsync(descriptor, autoSave: true, cancellationToken);
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
    /// address, so it stays fully random. <paramref name="mimeType"/> is the type the container <b>stores</b>
    /// (<see cref="GetStoredMimeType"/>): the detected type, or the one a converting contributor declared. The
    /// contributors, which reconcile the content with the BLOB name's extension inside <c>SaveAsync</c>, accept a JPEG
    /// under a <c>.webp</c> name: within one kind the content decides.
    /// </summary>
    protected virtual string CreateBlobName(string mimeType)
    {
        return Guid.NewGuid().ToString("N") + (DriveFileTypes.GetExtension(mimeType) ?? string.Empty);
    }

    /// <summary>
    /// The type the container <paramref name="containerName"/> stores an upload detected as <paramref name="mimeType"/>
    /// as: what its contributors declared (<c>GetStoredContentType</c> - a JPEG is stored as WebP on the images preset),
    /// else <paramref name="mimeType"/>. Known before the save, so the BLOB can be named after it; the descriptor still
    /// records the type detected from the stored bytes (<see cref="GetStoredSizeAndTypeAsync"/>).
    /// </summary>
    protected virtual string GetStoredMimeType(string containerName, string mimeType)
    {
        return BlobContainerConfigurationProvider.Get(containerName).GetStoredContentType(mimeType);
    }

    /// <summary>
    /// The name of a user-drive file whose upload, detected as <paramref name="uploadedMimeType"/>, is stored as
    /// <paramref name="storedMimeType"/>: <paramref name="name"/> with the canonical extension of the stored type
    /// (<c>photo.jpg</c> stored as WebP becomes <c>photo.webp</c>), so a download is named for what it is.
    /// <paramref name="name"/> itself when the types are the same or the stored type has no extension in
    /// <see cref="DriveFileTypes"/>. Not applied in a system container, whose names are the application's addresses.
    /// </summary>
    protected virtual string GetNameForStoredType(string name, string uploadedMimeType, string storedMimeType)
    {
        if (string.Equals(uploadedMimeType, storedMimeType, StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        var extension = DriveFileTypes.GetExtension(storedMimeType);
        if (extension == null)
        {
            return name;
        }

        var dot = name.LastIndexOf('.');
        var stem = dot >= 0 ? name[..dot] : name;
        var maxStem = FileDescriptorConsts.MaxNameLength - extension.Length;
        return (stem.Length <= maxStem ? stem : stem[..maxStem]) + extension;
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

    /// <summary>
    /// Gives <paramref name="existing"/> its name key back after an override failed to store the descriptor that was
    /// to replace it. Best-effort: when it fails too, the old file stays live and readable by name, only without the
    /// arbiter until it is next replaced or deleted - and the original exception is what the caller sees.
    /// </summary>
    protected virtual async Task TryClaimNameKeyAsync(FileDescriptor existing)
    {
        using var compensation = new CancellationTokenSource(CompensationTimeout);
        try
        {
            existing.ClaimNameKey();
            await FileDescriptorRepository.UpdateAsync(existing, autoSave: true, compensation.Token);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(
                exception,
                "The Drive could not give the name key back to the file {FileId} after an override failed.",
                existing.Id);
        }
    }

    /// <summary>
    /// The step of <see cref="DeleteAsync"/> that runs after commit: deletes the BLOB when no live descriptor names it,
    /// under the dedup lock for <paramref name="hash"/>. <paramref name="completingUnitOfWork"/> is the unit of work
    /// whose completion runs it - when it holds the lock already, the lock is not taken again.
    /// </summary>
    protected virtual async Task DeleteBlobIfUnusedAsync(
        Guid? tenantId,
        string containerName,
        string blobName,
        string hash,
        IUnitOfWork? completingUnitOfWork)
    {
        // Its own bounded token, as compensation has: the request's may be cancelled, or gone, by now. Long enough to
        // wait for the lock and then delete.
        using var cleanup = new CancellationTokenSource(DedupLockTimeout + CompensationTimeout);
        try
        {
            using (CurrentTenant.Change(tenantId))
            {
                var lockName = GetDedupLockName(tenantId, containerName, hash);

                IAbpDistributedLockHandle? handle = null;
                if (completingUnitOfWork == null || !IsDedupLockHeldBy(completingUnitOfWork, lockName))
                {
                    handle = await TryAcquireDedupLockAsync(lockName, cleanup.Token);
                    if (handle == null)
                    {
                        Logger.LogWarning(
                            "The Drive could not take the dedup lock {LockName} within {Timeout}; the BLOB {BlobName} in {ContainerName} is left in storage.",
                            lockName, DedupLockTimeout, blobName, containerName);
                        return;
                    }
                }

                await using (handle)
                {
                    bool inUse;
                    // A unit of work of its own: the completing one has committed and may not be used for more work.
                    using (var unitOfWork = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
                    {
                        inUse = await FileDescriptorRepository.IsBlobInUseAsync(containerName, blobName, cleanup.Token);
                        await unitOfWork.CompleteAsync(cleanup.Token);
                    }

                    if (inUse)
                    {
                        return;
                    }

                    await BlobContainerFactory.Create(containerName).DeleteAsync(blobName, cleanup.Token);
                }
            }
        }
        catch (Exception exception)
        {
            Logger.LogWarning(
                exception,
                "The Drive could not delete the BLOB {BlobName} in {ContainerName} after its last descriptor was deleted; it is left in storage.",
                blobName, containerName);
        }
    }

    protected virtual string GetDedupLockName(Guid? tenantId, string containerName, string hash)
    {
        return DedupLockName + FileDescriptor.CreateDedupKey(tenantId, containerName, hash);
    }

    /// <summary>
    /// Takes the dedup lock <paramref name="lockName"/> for the current unit of work - or reuses it, when that unit of
    /// work holds it already. With a unit of work the lock is released when it is disposed and
    /// <c>ReleaseOnReturn</c> is <c>null</c>; without one, <c>ReleaseOnReturn</c> is the handle, for the caller to
    /// release. <c>Acquired</c> is <c>false</c> when the lock could not be had within <see cref="DedupLockTimeout"/>.
    /// </summary>
    protected virtual async Task<(bool Acquired, IAbpDistributedLockHandle? ReleaseOnReturn)> AcquireDedupLockAsync(
        string lockName,
        CancellationToken cancellationToken)
    {
        var unitOfWork = UnitOfWorkManager.Current;
        if (unitOfWork != null && IsDedupLockHeldBy(unitOfWork, lockName))
        {
            return (true, null);
        }

        var handle = await TryAcquireDedupLockAsync(lockName, cancellationToken);
        if (handle == null)
        {
            return (false, null);
        }

        if (unitOfWork == null)
        {
            return (true, handle);
        }

        HoldDedupLock(unitOfWork, lockName, handle);
        return (true, null);
    }

    protected virtual Task<IAbpDistributedLockHandle?> TryAcquireDedupLockAsync(
        string lockName,
        CancellationToken cancellationToken)
    {
        return DistributedLock.TryAcquireAsync(lockName, DedupLockTimeout, cancellationToken);
    }

    protected virtual bool IsDedupLockHeldBy(IUnitOfWork unitOfWork, string lockName)
    {
        return unitOfWork.Items.TryGetValue(DedupLocksItemKey, out var item)
               && item is Dictionary<string, IAbpDistributedLockHandle> held
               && held.ContainsKey(lockName);
    }

    /// <summary>
    /// Keeps <paramref name="handle"/> with <paramref name="unitOfWork"/> until it is disposed - after its commit and
    /// its <c>OnCompleted</c> handlers, or after a rollback - so a row it inserted is settled before anyone else holds
    /// the lock.
    /// </summary>
    protected virtual void HoldDedupLock(IUnitOfWork unitOfWork, string lockName, IAbpDistributedLockHandle handle)
    {
        if (unitOfWork.Items.TryGetValue(DedupLocksItemKey, out var item)
            && item is Dictionary<string, IAbpDistributedLockHandle> alreadyHeld)
        {
            alreadyHeld[lockName] = handle;
            return;
        }

        var held = new Dictionary<string, IAbpDistributedLockHandle> { [lockName] = handle };
        unitOfWork.Items[DedupLocksItemKey] = held;

        // Taken now: the service scope may be on its way out by the time the unit of work is disposed.
        var logger = Logger;
        unitOfWork.Disposed += (_, _) => ReleaseDedupLocks(held, logger);
    }

    /// <summary>
    /// Releases the dedup locks a unit of work held. <c>IUnitOfWork.Disposed</c> is a synchronous event, so each
    /// asynchronous release is waited for here: the next waiter must find the lock free once the unit of work is gone.
    /// </summary>
    protected static void ReleaseDedupLocks(Dictionary<string, IAbpDistributedLockHandle> held, ILogger logger)
    {
        foreach (var (lockName, handle) in held)
        {
            try
            {
                AsyncHelper.RunSync(() => handle.DisposeAsync().AsTask());
            }
            catch (Exception exception)
            {
                // A distributed provider's lock expires on its own; the Drive must not fail a finished unit of work.
                logger.LogWarning(exception, "The Drive could not release the dedup lock {LockName}.", lockName);
            }
        }

        held.Clear();
    }
}
