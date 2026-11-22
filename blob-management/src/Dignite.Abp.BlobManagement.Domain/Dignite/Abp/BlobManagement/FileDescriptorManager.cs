using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Services;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// Stores files in Blob Management containers and removes them. The upload rules - size cap, allowed content types, image
/// processing - are the container's BLOB pipeline contributors, which run inside <c>IBlobContainer.SaveAsync</c>;
/// this class is the caller around them: it buffers and detects the upload, verifies its checksum when one was sent,
/// names the physical BLOB, writes the descriptor and keeps BLOB and descriptor from drifting apart.
/// <para>
/// It authorizes nobody: whoever calls it has been authorized by an application service, or is trusted in-process.
/// </para>
/// <para>
/// The two ways a file comes in meet here, in any managed container, and both are an application's: it saves a BLOB
/// under a name and reads it back by it (<see cref="SaveBlobAsync"/>, <see cref="FindBlobAsync"/>), or it keeps a file
/// by its id for its user (<see cref="CreateFileAsync"/>, <see cref="MoveAsync"/>, <see cref="RenameAsync"/>).
/// </para>
/// <para>
/// <b>One BLOB per file.</b> Every descriptor has a BLOB of its own, under a generated name, even when another file
/// holds the same content: no BLOB is shared, so none is counted twice or outlives the question of who still uses it.
/// A descriptor that is deleted is gone - there is no trash to bring it back from - and its BLOB goes with it.
/// </para>
/// <para>
/// <b>Consistency.</b> Bytes are written before the row that names them and removed after the row that named them:
/// a BLOB no row names any more - the one a deleted descriptor had, the one an overriding save replaced - is deleted
/// only after the unit of work has committed, so a rollback never leaves a row whose bytes are gone; and a BLOB written
/// in a unit of work that then fails is deleted with the change the rollback undoes
/// (<see cref="DeleteBlobOnRollback"/>), so no bytes are left without a descriptor.
/// </para>
/// <para>
/// Saves under one name take turns on the <b>name lock</b> (<see cref="IAbpDistributedLock"/>, named by the name key),
/// which a save holds until its unit of work is over, so the next one finds what it left: the row it inserted, or the
/// content it put in the row. Where the lock cannot be had, the database arbitrates - the unique name key between two
/// first saves, the row's concurrency stamp between two overriding ones - and the loser fails and its BLOB is deleted
/// again. ABP's default lock is in-process; several Blob Management instances need a distributed lock provider.
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
    /// How long a save under a name waits for the name lock. Bounded so a stuck holder costs a save that goes ahead on
    /// the database's arbitration alone, never a hung request.
    /// </summary>
    protected static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The name lock's name is this prefix and <see cref="FileDescriptor.CreateNameKey"/>.</summary>
    protected const string NameLockName = "Dignite.Abp.BlobManagement.Name:";

    /// <summary>
    /// The <see cref="IUnitOfWork.Items"/> entry with the locks a unit of work holds, by lock name: a unit of work that
    /// needs one of them again reuses it, since the lock is not reentrant and would otherwise wait for itself.
    /// </summary>
    protected const string LocksItemKey = "Dignite.Abp.BlobManagement.FileDescriptorManager.Locks";

    protected IFileDescriptorRepository FileDescriptorRepository { get; }

    protected IDirectoryDescriptorRepository DirectoryRepository { get; }

    protected IBlobContainerFactory BlobContainerFactory { get; }

    protected IBlobContainerConfigurationProvider BlobContainerConfigurationProvider { get; }

    protected IMimeTypeDetector MimeTypeDetector { get; }

    protected BlobManagementUsageManager UsageManager { get; }

    protected IAbpDistributedLock DistributedLock { get; }

    protected IUnitOfWorkManager UnitOfWorkManager { get; }

    protected IAuditPropertySetter AuditPropertySetter { get; }

    public FileDescriptorManager(
        IFileDescriptorRepository fileDescriptorRepository,
        IDirectoryDescriptorRepository directoryRepository,
        IBlobContainerFactory blobContainerFactory,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider,
        IMimeTypeDetector mimeTypeDetector,
        BlobManagementUsageManager usageManager,
        IAbpDistributedLock distributedLock,
        IUnitOfWorkManager unitOfWorkManager,
        IAuditPropertySetter auditPropertySetter)
    {
        FileDescriptorRepository = fileDescriptorRepository;
        DirectoryRepository = directoryRepository;
        BlobContainerFactory = blobContainerFactory;
        BlobContainerConfigurationProvider = blobContainerConfigurationProvider;
        MimeTypeDetector = mimeTypeDetector;
        UsageManager = usageManager;
        DistributedLock = distributedLock;
        UnitOfWorkManager = unitOfWorkManager;
        AuditPropertySetter = auditPropertySetter;
    }

    /// <summary>
    /// Stores <paramref name="stream"/> in the container <paramref name="containerName"/> under <paramref name="name"/>,
    /// the address the caller reads it back by, and records it: how an application saves a BLOB.
    /// <para>
    /// When a descriptor already has <paramref name="name"/>, the call fails with
    /// <see cref="BlobManagementErrorCodes.BlobAlreadyExists"/> unless <see cref="SaveBlobArgs.OverrideExisting"/> is
    /// set; then that descriptor takes the new content (<see cref="FileDescriptor.ReplaceContent"/>): it stays the
    /// same file - its id, its name, its key - in one write of one row, and the BLOB it had is deleted once the unit of
    /// work has committed. A save that fails leaves the file as it was.
    /// </para>
    /// <para>
    /// Saves under one name take turns on the name lock, taken before the lookup and held until the current unit of
    /// work is over (released when it is disposed, after commit or rollback; at the end of this method when there is
    /// none): the second one finds what the first one left. When the lock cannot be had within
    /// <see cref="LockTimeout"/> the save goes ahead without it, and the database is the arbiter that is left: the
    /// unique name key lets only one insert succeed, the row's concurrency stamp only one of two updates that read the
    /// same row, and the loser fails with the database provider's exception and its BLOB is deleted again.
    /// </para>
    /// <para>
    /// The bytes are stored as <see cref="StoreBlobAsync"/> stores them: buffered, detected, verified, checked against
    /// the tenant's allocation, saved under a new BLOB name through the container's contributors. The allocation is
    /// checked for the net change: the bytes the file held, still counted while the check runs, are the ones the save
    /// frees.
    /// </para>
    /// </summary>
    /// <param name="name">The caller's name for the file: the BLOB name it reads it back by. Its extension, if any, is
    /// checked against the content.</param>
    public virtual async Task<FileDescriptor> SaveBlobAsync(
        string containerName,
        string name,
        Stream stream,
        SaveBlobArgs? args = null,
        CancellationToken cancellationToken = default)
    {
        args ??= new SaveBlobArgs();
        GetBlobManagementContainerConfiguration(containerName);
        Check.NotNullOrWhiteSpace(name, nameof(name), FileDescriptorConsts.MaxNameLength);
        Check.NotNull(stream, nameof(stream));
        await UsageManager.CheckEnabledAsync();

        var nameKey = FileDescriptor.CreateNameKey(CurrentTenant.Id, containerName, name);
        var lockName = GetNameLockName(nameKey);
        var (locked, releaseOnReturn) = await AcquireLockAsync(lockName, cancellationToken);
        try
        {
            if (!locked)
            {
                Logger.LogWarning(
                    "Blob Management could not take the name lock {LockName} within {Timeout}; the save to {ContainerName} goes ahead on the database's arbitration alone.",
                    lockName, LockTimeout, containerName);
            }

            var existing = await FileDescriptorRepository.FindByNameKeyAsync(nameKey, cancellationToken);
            if (existing != null && !args.OverrideExisting)
            {
                throw new BusinessException(BlobManagementErrorCodes.BlobAlreadyExists)
                    .WithData("ContainerName", containerName)
                    .WithData("BlobName", name);
            }

            var stored = await StoreBlobAsync(
                containerName, name, stream, args.ExpectedSha256, releasedBytes: existing?.Size ?? 0, cancellationToken);

            if (existing != null)
            {
                return await ReplaceContentAsync(existing, stored, args, cancellationToken);
            }

            return await RecordAsync(containerName, stored, () =>
            {
                var file = new FileDescriptor(
                    GuidGenerator.Create(), containerName, name, stored.BlobName, stored.MimeType, stored.Size,
                    stored.Hash, args.ClientId, CurrentTenant.Id, nameIsAddress: true);
                file.SetPublic(args.IsPublic);
                if (args.CreatorId.HasValue)
                {
                    file.CreatorId = args.CreatorId;
                }

                return FileDescriptorRepository.InsertAsync(file, autoSave: true, cancellationToken);
            });
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
    /// Stores <paramref name="stream"/> as a new file named <paramref name="name"/> in the container
    /// <paramref name="containerName"/>, at its top or in <see cref="CreateFileArgs.DirectoryId"/>: how an application
    /// keeps a file for its user. The name need not be unique: the file is addressed by its id, and holds no
    /// <see cref="FileDescriptor.NameKey"/>. The bytes are stored as <see cref="SaveBlobAsync"/> stores them (see
    /// <see cref="StoreBlobAsync"/>). One difference: when the container stores the upload in another format (a JPEG as
    /// WebP), the name takes that format's extension (<see cref="GetNameForStoredType"/>).
    /// <para>
    /// The directory must be one of the container, which must allow directories
    /// (<see cref="BlobManagementErrorCodes.DirectoryNotFound"/>, <see cref="BlobManagementErrorCodes.DirectoriesNotAllowed"/>).
    /// </para>
    /// <para>
    /// An id given in advance (<see cref="CreateFileArgs.Id"/>, an upload ticket's) that a file already has fails on
    /// the primary key - after its bytes were stored, which are removed again as for any insert that fails.
    /// </para>
    /// </summary>
    public virtual async Task<FileDescriptor> CreateFileAsync(
        string containerName,
        string name,
        Stream stream,
        CreateFileArgs? args = null,
        CancellationToken cancellationToken = default)
    {
        args ??= new CreateFileArgs();
        GetBlobManagementContainerConfiguration(containerName);
        Check.NotNullOrWhiteSpace(name, nameof(name), FileDescriptorConsts.MaxNameLength);
        Check.NotNull(stream, nameof(stream));
        await UsageManager.CheckEnabledAsync();
        await CheckDirectoryAsync(containerName, args.DirectoryId, cancellationToken);

        var stored = await StoreBlobAsync(
            containerName, name, stream, args.ExpectedSha256, releasedBytes: 0, cancellationToken);

        return await RecordAsync(containerName, stored, () =>
        {
            var file = new FileDescriptor(
                args.Id ?? GuidGenerator.Create(), containerName,
                GetNameForStoredType(name, stored.UploadedMimeType, stored.MimeType), stored.BlobName, stored.MimeType,
                stored.Size, stored.Hash, args.ClientId, CurrentTenant.Id);
            file.SetDirectory(args.DirectoryId);
            file.SetPublic(args.IsPublic);
            if (args.IsPending)
            {
                file.MarkPending();
            }

            if (args.CreatorId.HasValue)
            {
                file.CreatorId = args.CreatorId;
            }

            foreach (var (propertyName, value) in args.ExtraProperties)
            {
                file.SetProperty(propertyName, value);
            }

            return FileDescriptorRepository.InsertAsync(file, autoSave: true, cancellationToken);
        });
    }

    /// <summary>
    /// Deletes the descriptor in the current unit of work, then - once that unit of work has committed - its BLOB.
    /// <para>
    /// The row is removed, not marked: there is no trash, and bytes that are gone cannot be restored. The BLOB step is
    /// registered with <c>IUnitOfWork.OnCompleted</c>, which runs only after a successful commit: a rollback brings the
    /// row back with its bytes. Without a unit of work it runs at once.
    /// </para>
    /// <para>
    /// The step is best-effort: a failure is logged and leaves an unreferenced BLOB - unreachable bytes, never lost
    /// data - and does not surface to a request whose transaction already committed. ABP's BLOB containers cannot be
    /// enumerated, so reclaiming such orphans belongs to tooling on the concrete storage backend.
    /// </para>
    /// </summary>
    public virtual async Task DeleteAsync(FileDescriptor file, CancellationToken cancellationToken = default)
    {
        Check.NotNull(file, nameof(file));

        await FileDescriptorRepository.DeleteAsync(file, autoSave: true, cancellationToken);
        await DeleteBlobAfterCommitAsync(file.TenantId, file.ContainerName, file.BlobName);
    }

    /// <summary>
    /// Files <paramref name="file"/> under <paramref name="directoryId"/> (<c>null</c>: the top of its container). The
    /// directory must be one of the file's container (<see cref="BlobManagementErrorCodes.DirectoryNotFound"/>), so a
    /// file never changes containers. A file addressed by its name is not moved
    /// (<see cref="BlobManagementErrorCodes.FileAddressedByName"/>).
    /// </summary>
    public virtual async Task<FileDescriptor> MoveAsync(
        FileDescriptor file,
        Guid? directoryId,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(file, nameof(file));
        CheckNotAddressedByName(file);
        await CheckDirectoryAsync(file.ContainerName, directoryId, cancellationToken);

        if (file.DirectoryId == directoryId)
        {
            return file;
        }

        file.SetDirectory(directoryId);
        return await FileDescriptorRepository.UpdateAsync(file, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Renames a file kept by its id, whose name is a display name and need not be unique. A file addressed by its
    /// name is not renamed: the name is how the application that saved it reads it back
    /// (<see cref="BlobManagementErrorCodes.FileAddressedByName"/>).
    /// </summary>
    public virtual async Task<FileDescriptor> RenameAsync(
        FileDescriptor file,
        string name,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(file, nameof(file));
        Check.NotNullOrWhiteSpace(name, nameof(name), FileDescriptorConsts.MaxNameLength);
        CheckNotAddressedByName(file);

        if (file.Name == name)
        {
            return file;
        }

        file.Rename(name);
        return await FileDescriptorRepository.UpdateAsync(file, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Confirms a file that came in through an upload ticket (<see cref="FileDescriptor.IsPending"/>): its business
    /// system filed it with a record of its own, so it is listed, can be read by link and is no longer deleted as
    /// abandoned. A file that is not pending is left as it is.
    /// </summary>
    public virtual async Task<FileDescriptor> ConfirmAsync(FileDescriptor file, CancellationToken cancellationToken = default)
    {
        Check.NotNull(file, nameof(file));
        if (!file.IsPending)
        {
            return file;
        }

        file.Confirm();
        return await FileDescriptorRepository.UpdateAsync(file, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Marks a file kept by its id public or not (<see cref="FileDescriptor.IsPublic"/>). A file addressed by its name
    /// is not changed here (<see cref="BlobManagementErrorCodes.FileAddressedByName"/>): its application's container
    /// declares it, and each save sets it.
    /// </summary>
    public virtual async Task<FileDescriptor> SetPublicAsync(
        FileDescriptor file,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(file, nameof(file));
        CheckNotAddressedByName(file);

        if (file.IsPublic == isPublic)
        {
            return file;
        }

        file.SetPublic(isPublic);
        return await FileDescriptorRepository.UpdateAsync(file, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// The descriptor an application saved under <paramref name="name"/> in <paramref name="containerName"/>, or
    /// <c>null</c>. Found by the name key, so the name is compared as it was given, whatever the database's collation.
    /// </summary>
    public virtual async Task<FileDescriptor?> FindBlobAsync(
        string containerName,
        string name,
        CancellationToken cancellationToken = default)
    {
        GetBlobManagementContainerConfiguration(containerName);
        if (name.IsNullOrWhiteSpace())
        {
            return null;
        }

        return await FileDescriptorRepository.FindByNameKeyAsync(
            FileDescriptor.CreateNameKey(CurrentTenant.Id, containerName, name), cancellationToken);
    }

    /// <summary>
    /// The confirmed public file kept by id in <paramref name="containerName"/> whose physical BLOB is
    /// <paramref name="blobName"/>, or <c>null</c>: the public address of a file kept by id names its BLOB, since its
    /// name need not be unique. A file saved under a name has its name as its address, and does not open this one.
    /// </summary>
    public virtual async Task<FileDescriptor?> FindPublicByBlobNameAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        GetBlobManagementContainerConfiguration(containerName);
        if (blobName.IsNullOrWhiteSpace())
        {
            return null;
        }

        return await FileDescriptorRepository.FindPublicByBlobNameAsync(containerName, blobName, cancellationToken);
    }

    /// <summary>
    /// The descriptor that stands where <paramref name="stale"/> was read, when an overriding save has put another BLOB
    /// in the file since. A read that found <paramref name="stale"/> and then no bytes under its BLOB name was overtaken
    /// by such a save, which deleted that BLOB with its commit: the file is there, with other bytes, and the read
    /// answers from this descriptor instead of reporting a file that is missing.
    /// <para>
    /// Read in a unit of work of its own, so that it is what the database holds now and not the instance the caller's
    /// unit of work tracks. <c>null</c> when the file is gone, when it still names the same BLOB - which is then simply
    /// missing from storage - and for a file kept by id, whose bytes are never replaced.
    /// </para>
    /// </summary>
    public virtual async Task<FileDescriptor?> FindReplacedBlobAsync(
        FileDescriptor stale,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(stale, nameof(stale));
        if (stale.NameKey == null)
        {
            return null;
        }

        FileDescriptor? current;
        using (var unitOfWork = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
        {
            current = await FileDescriptorRepository.FindByNameKeyAsync(stale.NameKey, cancellationToken);
            await unitOfWork.CompleteAsync(cancellationToken);
        }

        return current != null && current.BlobName != stale.BlobName ? current : null;
    }

    /// <summary>The bytes of <paramref name="file"/>, or <c>null</c> when its BLOB is missing from storage.</summary>
    public virtual Task<Stream?> GetStreamOrNullAsync(FileDescriptor file, CancellationToken cancellationToken = default)
    {
        return BlobContainerFactory.Create(file.ContainerName).GetOrNullAsync(file.BlobName, cancellationToken);
    }

    /// <summary>
    /// The Blob Management configuration of <paramref name="containerName"/>; fails with
    /// <see cref="BlobManagementErrorCodes.ContainerNotFound"/> for a container the Blob Management host did not declare.
    /// </summary>
    public virtual BlobManagementContainerConfiguration GetBlobManagementContainerConfiguration(string containerName)
    {
        return BlobContainerConfigurationProvider.GetBlobManagementContainerConfiguration(containerName);
    }

    /// <summary>
    /// Whether <paramref name="directoryId"/> may hold a file of <paramref name="containerName"/>: it is a directory of
    /// that container, which allows directories. <c>null</c>, the top of the container, always may.
    /// </summary>
    protected virtual async Task CheckDirectoryAsync(
        string containerName,
        Guid? directoryId,
        CancellationToken cancellationToken)
    {
        if (!directoryId.HasValue)
        {
            return;
        }

        if (!GetBlobManagementContainerConfiguration(containerName).AllowDirectories)
        {
            throw new BusinessException(BlobManagementErrorCodes.DirectoriesNotAllowed)
                .WithData("ContainerName", containerName);
        }

        var directory = await DirectoryRepository.FindAsync(directoryId.Value, false, cancellationToken);
        if (directory == null || directory.ContainerName != containerName)
        {
            throw new BusinessException(BlobManagementErrorCodes.DirectoryNotFound);
        }
    }

    protected virtual void CheckNotAddressedByName(FileDescriptor file)
    {
        if (file.IsAddressedByName)
        {
            throw new BusinessException(BlobManagementErrorCodes.FileAddressedByName).WithData("Name", file.Name);
        }
    }

    /// <summary>What <see cref="StoreBlobAsync"/> put in storage, for the descriptor that is about to name it.</summary>
    /// <param name="BlobName">The physical BLOB's name, generated.</param>
    /// <param name="MimeType">The type of the stored bytes, detected from them.</param>
    /// <param name="Size">The size of the stored bytes, after the container's contributors ran.</param>
    /// <param name="Hash">The SHA-256 (base64) of the uploaded bytes when the caller sent one and it was verified;
    /// otherwise <c>null</c>.</param>
    /// <param name="UploadedMimeType">The type of the bytes as they were uploaded, before a contributor may have stored
    /// them in another format.</param>
    protected sealed record StoredBlob(string BlobName, string MimeType, long Size, string? Hash, string UploadedMimeType);

    /// <summary>
    /// Buffers, detects and verifies the upload and saves it under a new BLOB name; the caller then writes the
    /// descriptor that names it (<see cref="RecordAsync"/>).
    /// <para>
    /// The upload is used as it is when it is already seekable at its start, and otherwise copied once into a seekable
    /// buffer - memory, or a temporary file past <see cref="BlobStoringPipelineConsts.MemoryBufferThreshold"/>
    /// (<see cref="BlobStreamBuffering.EnsureSeekableAsync(Stream, long, CancellationToken)"/>); either way it is capped at
    /// what the container's pipeline enforces (its <c>MaxSize</c> contributor, else
    /// <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/>), so an oversized upload fails with
    /// <c>Dignite.Abp.BlobStoring:ContentTooLarge</c> before anything is stored. Its type is detected from the
    /// bytes and reconciled with the extension of <paramref name="name"/>
    /// (<c>Dignite.Abp.BlobStoring:ContentTypeMismatch</c>).
    /// </para>
    /// <para>
    /// It is hashed only when the caller sent a checksum to verify (<paramref name="expectedSha256"/>): SHA-256, taken
    /// while the bytes are buffered when the upload is not seekable, so it adds nothing after the last byte, and
    /// otherwise in one pass over the seekable content. A hash that differs fails with
    /// <see cref="BlobManagementErrorCodes.ChecksumMismatch"/> before anything is stored; one that is not base64 of 32
    /// bytes, with <see cref="BlobManagementErrorCodes.ChecksumInvalid"/> before the upload is read. The verified value
    /// becomes the descriptor's <see cref="FileDescriptor.Hash"/>; without one it is <c>null</c>, and no hash is
    /// computed. It is the hash of the bytes the caller sent, before the container's contributors transformed them.
    /// </para>
    /// <para>
    /// The tenant's allocation is checked next: content is refused when it would take the tenant past its storage size
    /// (<see cref="BlobManagementErrorCodes.StorageQuotaExceeded"/>, measured by the upload's length less
    /// <paramref name="releasedBytes"/>, see <see cref="BlobManagementUsageManager"/>).
    /// </para>
    /// <para>
    /// Then the BLOB is saved under a new name (<see cref="CreateBlobName"/>) without overwriting - the contributors run
    /// here and may reject the upload. A container may store content in another format than it was uploaded in - the
    /// images preset stores a JPEG, and most PNGs, as WebP: the physical BLOB is named for the stored type, which the
    /// container declares (<see cref="GetStoredMimeType"/>), and the result carries the type and size of the stored
    /// bytes.
    /// </para>
    /// </summary>
    /// <param name="releasedBytes">The bytes the save frees once it is done - those an overriding save replaces - which
    /// still count while the allocation is checked.</param>
    /// <param name="expectedSha256">The SHA-256 (base64) the caller says the bytes have; <c>null</c> asks for no check.</param>
    protected virtual async Task<StoredBlob> StoreBlobAsync(
        string containerName,
        string name,
        Stream stream,
        string? expectedSha256,
        long releasedBytes,
        CancellationToken cancellationToken)
    {
        // Read before the upload is: a value that cannot be checked fails without a byte buffered.
        var expectedHash = expectedSha256 == null ? null : BlobManagementChecksum.ParseSha256(expectedSha256);

        // A stream that is not seekable is copied below, and hashed on the way; a seekable one is used as it is and
        // hashed in a pass of its own after.
        using var hashing = expectedHash != null && !stream.CanSeek ? new BlobManagementHashingStream(stream) : null;
        Stream source = hashing ?? stream;

        var maxBytes = BlobStreamBuffering.GetMaxBufferedBytes(BlobContainerConfigurationProvider.Get(containerName));
        var content = await BlobStreamBuffering.EnsureSeekableAsync(source, maxBytes, cancellationToken);
        // A copy is this method's to dispose; the caller's own stream, used as it is, stays the caller's.
        await using var ownedCopy = ReferenceEquals(content, source) ? null : content;

        string? hash = null;
        if (expectedHash != null)
        {
            var actualHash = hashing?.GetHash() ?? await ComputeHashAsync(content, cancellationToken);
            hash = Convert.ToBase64String(actualHash);
            if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            {
                throw new BusinessException(BlobManagementErrorCodes.ChecksumMismatch)
                    .WithData("Expected", Convert.ToBase64String(expectedHash))
                    .WithData("Actual", hash);
            }
        }

        var mimeType = await MimeTypeDetector.DetectAsync(content, name, cancellationToken);

        // Taken before the save: the content belongs to the pipeline while it runs.
        var uploadLength = content.Length;
        await UsageManager.CheckCanStoreAsync(uploadLength, releasedBytes, cancellationToken);

        // Named for what the container stores: a JPEG it stores as WebP gets a .webp BLOB.
        var storedMimeType = GetStoredMimeType(containerName, mimeType, content);
        var blobName = CreateBlobName(storedMimeType);
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
            await TryDeleteBlobAsync(containerName, blobName);
            throw;
        }

        try
        {
            var (size, storedType) = await GetStoredSizeAndTypeAsync(
                container, blobName, storedMimeType, uploadLength, cancellationToken);
            return new StoredBlob(blobName, storedType, size, hash, mimeType);
        }
        catch
        {
            await TryDeleteBlobAsync(containerName, blobName);
            throw;
        }
    }

    /// <summary>
    /// Runs <paramref name="write"/>, which writes the descriptor that names <paramref name="stored"/> - an insert, or
    /// the update of an overriding save - and keeps the BLOB from outliving a write that does not last: it is deleted
    /// at once when the write fails (a unique key, the primary key, a concurrency stamp, an argument the descriptor
    /// refuses), and when the unit of work fails later and rolls the write back (<see cref="DeleteBlobOnRollback"/>).
    /// </summary>
    protected virtual async Task<FileDescriptor> RecordAsync(
        string containerName,
        StoredBlob stored,
        Func<Task<FileDescriptor>> write)
    {
        FileDescriptor file;
        try
        {
            file = await write();
        }
        catch
        {
            await TryDeleteBlobAsync(containerName, stored.BlobName);
            throw;
        }

        DeleteBlobOnRollback(file.TenantId, containerName, stored.BlobName);
        return file;
    }

    /// <summary>
    /// Puts <paramref name="stored"/> in <paramref name="existing"/>, the descriptor an overriding save found under its
    /// name: one update of one row, after which the BLOB the file had is deleted - once the unit of work has
    /// committed, as <see cref="DeleteAsync"/> deletes one. The file is then counted for whoever saved it now, from
    /// now: <see cref="FileDescriptor.CreationTime"/> and <see cref="FileDescriptor.CreatorId"/> are set as ABP's
    /// auditing sets them on an insert, unless the caller named the user.
    /// <para>
    /// When the update fails, the descriptor is given back what it held, in memory as well: a unit of work that goes
    /// on after the failure must not write the new BLOB's name with a later save, since that BLOB is deleted.
    /// </para>
    /// </summary>
    protected virtual async Task<FileDescriptor> ReplaceContentAsync(
        FileDescriptor existing,
        StoredBlob stored,
        SaveBlobArgs args,
        CancellationToken cancellationToken)
    {
        var replacedBlobName = existing.BlobName;
        var previous = (
            existing.BlobName, existing.MimeType, existing.Size, existing.Hash, existing.ClientId, existing.IsPublic,
            existing.CreationTime, existing.CreatorId);

        var file = await RecordAsync(existing.ContainerName, stored, async () =>
        {
            try
            {
                existing.ReplaceContent(
                    stored.BlobName, stored.MimeType, stored.Size, stored.Hash, args.ClientId, args.IsPublic,
                    creationTime: default, args.CreatorId);
                AuditPropertySetter.SetCreationProperties(existing);
                return await FileDescriptorRepository.UpdateAsync(existing, autoSave: true, cancellationToken);
            }
            catch
            {
                existing.ReplaceContent(
                    previous.BlobName, previous.MimeType, previous.Size, previous.Hash, previous.ClientId,
                    previous.IsPublic, previous.CreationTime, previous.CreatorId);
                throw;
            }
        });

        await DeleteBlobAfterCommitAsync(file.TenantId, file.ContainerName, replacedBlobName);
        return file;
    }

    /// <summary>
    /// Has the BLOB <paramref name="blobName"/>, which a descriptor was just written to name, deleted again when the
    /// current unit of work fails and takes that write with it: the caller's own work after the save threw, it rolled
    /// back, or the commit failed. The bytes would otherwise stay in storage with no descriptor - counted for nobody,
    /// found by nothing.
    /// <para>
    /// Only where a failure does undo the write (<see cref="IsRolledBackOnFailure"/>), and never once the unit of work
    /// has committed: ABP raises <c>Failed</c> for an <c>OnCompleted</c> handler that throws as well, after the commit,
    /// when the row is there to stay. Without a unit of work the write is final, and nothing is registered.
    /// </para>
    /// </summary>
    protected virtual void DeleteBlobOnRollback(Guid? tenantId, string containerName, string blobName)
    {
        var unitOfWork = UnitOfWorkManager.Current;
        if (unitOfWork == null || !IsRolledBackOnFailure(unitOfWork))
        {
            return;
        }

        // Resolved now: the failure may be reported while the service scope is on its way out.
        _ = Logger;

        // IUnitOfWork.Failed is a synchronous event, so the asynchronous delete is waited for here - before the unit of
        // work releases the name lock (its Disposed event comes after), so the next save under the name finds neither
        // the write nor the bytes.
        unitOfWork.Failed += (_, args) =>
        {
            if (!args.UnitOfWork.IsCompleted)
            {
                AsyncHelper.RunSync(() => DeleteBlobAsync(tenantId, containerName, blobName));
            }
        };
    }

    /// <summary>
    /// Whether a write made in <paramref name="unitOfWork"/> is gone when it fails: a transactional unit of work
    /// rolls its changes back, any other has written them for good by then.
    /// </summary>
    protected virtual bool IsRolledBackOnFailure(IUnitOfWork unitOfWork)
    {
        return unitOfWork.Options.IsTransactional;
    }

    /// <summary>
    /// Deletes the BLOB <paramref name="blobName"/>, which no descriptor names any more once the current unit of work
    /// commits - its descriptor was deleted, or an overriding save put another BLOB in it: after that commit
    /// (<c>IUnitOfWork.OnCompleted</c>), so a rollback keeps the bytes the restored row names; at once without a unit of
    /// work. The names are the caller's to capture: the step may run after it has moved on from the entity and the
    /// tenant.
    /// </summary>
    protected virtual async Task DeleteBlobAfterCommitAsync(Guid? tenantId, string containerName, string blobName)
    {
        var unitOfWork = UnitOfWorkManager.Current;
        if (unitOfWork == null)
        {
            await DeleteBlobAsync(tenantId, containerName, blobName);
            return;
        }

        unitOfWork.OnCompleted(() => DeleteBlobAsync(tenantId, containerName, blobName));
    }

    /// <summary>
    /// A new physical BLOB name: 32 hexadecimal digits of a random GUID plus the canonical extension of
    /// <paramref name="mimeType"/> from <see cref="BlobManagementFileTypes"/> (none when the table has no row for it).
    /// <see cref="Guid.NewGuid"/> rather than the sequential <c>IGuidGenerator</c>: the name may become a public
    /// address, so it stays fully random. <paramref name="mimeType"/> is the type the container <b>stores</b>
    /// (<see cref="GetStoredMimeType"/>): the detected type, or the one a converting contributor declared. The
    /// contributors, which reconcile the content with the BLOB name's extension inside <c>SaveAsync</c>, accept a JPEG
    /// under a <c>.webp</c> name: within one kind the content decides.
    /// </summary>
    protected virtual string CreateBlobName(string mimeType)
    {
        return Guid.NewGuid().ToString("N") + (BlobManagementFileTypes.GetExtension(mimeType) ?? string.Empty);
    }

    /// <summary>
    /// The type the container <paramref name="containerName"/> stores <paramref name="content"/>, an upload detected as
    /// <paramref name="mimeType"/>, as: what its contributors declared for it (<c>GetStoredContentType</c> - on the images
    /// preset a JPEG is stored as WebP, and a PNG unless it is small, 16-bit or animated), else <paramref name="mimeType"/>.
    /// Known before the save - the declaration reads the content's header and length, as the contributor in the save
    /// does - so the BLOB can be named after it; the descriptor still records the type detected from the stored bytes
    /// (<see cref="GetStoredSizeAndTypeAsync"/>). <paramref name="content"/> is seekable; its position is restored.
    /// </summary>
    protected virtual string GetStoredMimeType(string containerName, string mimeType, Stream content)
    {
        return BlobContainerConfigurationProvider.Get(containerName).GetStoredContentType(mimeType, content);
    }

    /// <summary>
    /// The name of a file kept by id whose upload, detected as <paramref name="uploadedMimeType"/>, is stored as
    /// <paramref name="storedMimeType"/>: <paramref name="name"/> with the canonical extension of the stored type
    /// (<c>photo.jpg</c> stored as WebP becomes <c>photo.webp</c>), so a download is named for what it is.
    /// <paramref name="name"/> itself when the types are the same or the stored type has no extension in
    /// <see cref="BlobManagementFileTypes"/>. Not applied to a file an application saved under a name, which is its
    /// address.
    /// </summary>
    protected virtual string GetNameForStoredType(string name, string uploadedMimeType, string storedMimeType)
    {
        if (string.Equals(uploadedMimeType, storedMimeType, StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        var extension = BlobManagementFileTypes.GetExtension(storedMimeType);
        if (extension == null)
        {
            return name;
        }

        var dot = name.LastIndexOf('.');
        var stem = dot >= 0 ? name[..dot] : name;
        var maxStem = FileDescriptorConsts.MaxNameLength - extension.Length;
        return (stem.Length <= maxStem ? stem : stem[..maxStem]) + extension;
    }

    /// <summary>SHA-256 of the content; the stream is rewound afterwards. Called only when a checksum is to be verified.</summary>
    protected virtual async Task<byte[]> ComputeHashAsync(Stream content, CancellationToken cancellationToken)
    {
        content.Position = 0;
        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        content.Position = 0;
        return hash;
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

    /// <summary>
    /// Deletes a BLOB whose descriptor was not written, in the current tenant, on a bounded token of its own.
    /// Best-effort: the compensation must not hide the exception that asked for it.
    /// </summary>
    protected virtual async Task TryDeleteBlobAsync(string containerName, string blobName)
    {
        using var compensation = new CancellationTokenSource(CompensationTimeout);
        try
        {
            await BlobContainerFactory.Create(containerName).DeleteAsync(blobName, compensation.Token);
        }
        catch
        {
            // Best-effort compensation must not hide the original exception.
        }
    }

    /// <summary>
    /// Deletes a BLOB that no descriptor names any more, in its tenant, on a bounded token of its own - the request's
    /// may be cancelled, or gone, by now. Best-effort: a failure is logged and leaves the bytes in storage.
    /// </summary>
    protected virtual async Task DeleteBlobAsync(Guid? tenantId, string containerName, string blobName)
    {
        using var cleanup = new CancellationTokenSource(CompensationTimeout);
        try
        {
            using (CurrentTenant.Change(tenantId))
            {
                await BlobContainerFactory.Create(containerName).DeleteAsync(blobName, cleanup.Token);
            }
        }
        catch (Exception exception)
        {
            Logger.LogWarning(
                exception,
                "Blob Management could not delete the BLOB {BlobName} in {ContainerName}, which no descriptor names any more; it is left in storage.",
                blobName, containerName);
        }
    }

    protected virtual string GetNameLockName(string nameKey)
    {
        return NameLockName + nameKey;
    }

    /// <summary>
    /// Takes the lock <paramref name="lockName"/> for the current unit of work - or reuses it, when that unit of
    /// work holds it already. With a unit of work the lock is released when it is disposed and
    /// <c>ReleaseOnReturn</c> is <c>null</c>; without one, <c>ReleaseOnReturn</c> is the handle, for the caller to
    /// release. <c>Acquired</c> is <c>false</c> when the lock could not be had within <see cref="LockTimeout"/>.
    /// </summary>
    protected virtual async Task<(bool Acquired, IAbpDistributedLockHandle? ReleaseOnReturn)> AcquireLockAsync(
        string lockName,
        CancellationToken cancellationToken)
    {
        var unitOfWork = UnitOfWorkManager.Current;
        if (unitOfWork != null && IsLockHeldBy(unitOfWork, lockName))
        {
            return (true, null);
        }

        var handle = await TryAcquireLockAsync(lockName, cancellationToken);
        if (handle == null)
        {
            return (false, null);
        }

        if (unitOfWork == null)
        {
            return (true, handle);
        }

        HoldLock(unitOfWork, lockName, handle);
        return (true, null);
    }

    protected virtual Task<IAbpDistributedLockHandle?> TryAcquireLockAsync(
        string lockName,
        CancellationToken cancellationToken)
    {
        return DistributedLock.TryAcquireAsync(lockName, LockTimeout, cancellationToken);
    }

    protected virtual bool IsLockHeldBy(IUnitOfWork unitOfWork, string lockName)
    {
        return unitOfWork.Items.TryGetValue(LocksItemKey, out var item)
               && item is Dictionary<string, IAbpDistributedLockHandle> held
               && held.ContainsKey(lockName);
    }

    /// <summary>
    /// Keeps <paramref name="handle"/> with <paramref name="unitOfWork"/> until it is disposed - after its commit and
    /// its <c>OnCompleted</c> handlers, or after a rollback - so a row it wrote is settled before anyone else holds
    /// the lock.
    /// </summary>
    protected virtual void HoldLock(IUnitOfWork unitOfWork, string lockName, IAbpDistributedLockHandle handle)
    {
        if (unitOfWork.Items.TryGetValue(LocksItemKey, out var item)
            && item is Dictionary<string, IAbpDistributedLockHandle> alreadyHeld)
        {
            alreadyHeld[lockName] = handle;
            return;
        }

        var held = new Dictionary<string, IAbpDistributedLockHandle> { [lockName] = handle };
        unitOfWork.Items[LocksItemKey] = held;

        // Taken now: the service scope may be on its way out by the time the unit of work is disposed.
        var logger = Logger;
        unitOfWork.Disposed += (_, _) => ReleaseLocks(held, logger);
    }

    /// <summary>
    /// Releases the locks a unit of work held. <c>IUnitOfWork.Disposed</c> is a synchronous event, so each
    /// asynchronous release is waited for here: the next waiter must find the lock free once the unit of work is gone.
    /// </summary>
    protected static void ReleaseLocks(Dictionary<string, IAbpDistributedLockHandle> held, ILogger logger)
    {
        foreach (var (lockName, handle) in held)
        {
            try
            {
                AsyncHelper.RunSync(() => handle.DisposeAsync().AsTask());
            }
            catch (Exception exception)
            {
                // A distributed provider's lock expires on its own; Blob Management must not fail a finished unit of work.
                logger.LogWarning(exception, "Blob Management could not release the lock {LockName}.", lockName);
            }
        }

        held.Clear();
    }
}
