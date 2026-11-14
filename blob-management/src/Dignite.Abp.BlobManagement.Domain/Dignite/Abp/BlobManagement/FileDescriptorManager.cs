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

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// Stores files in Blob Management containers and removes them. The upload rules - size cap, allowed content types, image
/// processing - are the container's BLOB pipeline contributors, which run inside <c>IBlobContainer.SaveAsync</c>;
/// this class is the caller around them: it buffers and detects the upload, hashes it, names the physical BLOB,
/// writes the descriptor and keeps BLOB and descriptor from drifting apart.
/// <para>
/// It authorizes nobody: whoever calls it has been authorized by an application service, or is trusted in-process.
/// </para>
/// <para>
/// The two ways a file comes in meet here, in any managed container: an application saves a BLOB under a name and
/// reads it back by it (<see cref="CreateAsync"/>, <see cref="FindAsync"/>), a person uploads a file and it is
/// addressed by its id (<see cref="UploadAsync"/>, <see cref="MoveAsync"/>, <see cref="RenameAsync"/>).
/// </para>
/// <para>
/// <b>One BLOB per file.</b> Every descriptor writes a BLOB of its own, under a new name, even when another file holds
/// the same content: no BLOB is shared, so none is counted twice or outlives the question of who still uses it.
/// </para>
/// <para>
/// <b>Consistency.</b> A physical BLOB is deleted only after the unit of work that deleted its descriptor has
/// committed (<see cref="DeleteAsync"/>), so a rollback never brings back a row whose bytes are gone. Saves under one
/// name take turns on the <b>name lock</b> (<see cref="IAbpDistributedLock"/>, named by the name key), which a save holds
/// until its unit of work is over: an overriding save takes the address from the descriptor it replaces before it
/// stores the new one, and a save that looked the name up in between would find no holder. ABP's default lock is
/// in-process; several Blob Management instances need a distributed lock provider.
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
    /// the unique name key alone, never a hung request.
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

    protected IBlobContainerFactory BlobContainerFactory { get; }

    protected IBlobContainerConfigurationProvider BlobContainerConfigurationProvider { get; }

    protected IMimeTypeDetector MimeTypeDetector { get; }

    protected BlobManagementUsageManager UsageManager { get; }

    // Resolved lazily, not injected, so the constructor - which subclasses repeat - stays as it was.
    protected IDirectoryDescriptorRepository DirectoryRepository =>
        LazyServiceProvider.LazyGetRequiredService<IDirectoryDescriptorRepository>();

    protected IAbpDistributedLock DistributedLock => LazyServiceProvider.LazyGetRequiredService<IAbpDistributedLock>();

    protected IUnitOfWorkManager UnitOfWorkManager => LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>();

    public FileDescriptorManager(
        IFileDescriptorRepository fileDescriptorRepository,
        IBlobContainerFactory blobContainerFactory,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider,
        IMimeTypeDetector mimeTypeDetector,
        BlobManagementUsageManager usageManager)
    {
        FileDescriptorRepository = fileDescriptorRepository;
        BlobContainerFactory = blobContainerFactory;
        BlobContainerConfigurationProvider = blobContainerConfigurationProvider;
        MimeTypeDetector = mimeTypeDetector;
        UsageManager = usageManager;
    }

    /// <summary>
    /// Stores <paramref name="stream"/> in the container <paramref name="containerName"/> under <paramref name="name"/>,
    /// the address the caller reads it back by, and records it: how an application saves a BLOB.
    /// <para>
    /// When a live descriptor already has <paramref name="name"/>, the call fails with
    /// <see cref="BlobManagementErrorCodes.BlobAlreadyExists"/> unless <paramref name="overrideExisting"/> is set; then the new
    /// descriptor replaces it: the old one gives up its <see cref="FileDescriptor.NameKey"/> (saved at once - the
    /// key is unique, so both cannot hold it), the new one is stored, and the old one is deleted as
    /// <see cref="DeleteAsync"/> deletes. If the new one cannot be stored, the old one takes its key back.
    /// </para>
    /// <para>
    /// Saves under one name take turns on the name lock, taken before the lookup and held until the current unit of
    /// work is over (released when it is disposed, after commit or rollback; at the end of this method when there is
    /// none): the second one finds what the first one left. When the lock cannot be had within
    /// <see cref="LockTimeout"/> the save goes ahead without it, and the unique name key is the arbiter that is left:
    /// it lets only one insert succeed, and the loser fails with the database provider's exception and its BLOB is
    /// deleted again.
    /// </para>
    /// <para>
    /// The file is stored as <see cref="StoreAsync"/> stores: buffered, detected, hashed, checked against the tenant's
    /// allocation, saved under a new BLOB name through the container's contributors, recorded. A replaced descriptor's
    /// bytes still count while the allocation is checked.
    /// </para>
    /// </summary>
    /// <param name="name">The caller's name for the file: the BLOB name it reads it back by. Its extension, if any, is
    /// checked against the content.</param>
    /// <param name="clientId">The OAuth client writing through the BLOB API, recorded on the descriptor.</param>
    /// <param name="creatorId">The user the BLOB is counted for; <c>null</c> leaves it to ABP's auditing - the current
    /// user, if there is one.</param>
    /// <param name="isPublic">Whether anyone may read the BLOB at its public address (<see cref="FileDescriptor.IsPublic"/>):
    /// the application's container declares it, and each save says it again, so an overriding save sets it anew.</param>
    public virtual async Task<FileDescriptor> CreateAsync(
        string containerName,
        string name,
        Stream stream,
        bool overrideExisting = false,
        string? clientId = null,
        Guid? creatorId = null,
        bool isPublic = false,
        CancellationToken cancellationToken = default)
    {
        GetBlobManagementContainerConfiguration(containerName);
        Check.NotNullOrWhiteSpace(name, nameof(name), FileDescriptorConsts.MaxNameLength);
        Check.NotNull(stream, nameof(stream));
        await UsageManager.CheckEnabledAsync();

        var lockName = GetNameLockName(CurrentTenant.Id, containerName, name);
        var (locked, releaseOnReturn) = await AcquireLockAsync(lockName, cancellationToken);
        try
        {
            if (!locked)
            {
                Logger.LogWarning(
                    "Blob Management could not take the name lock {LockName} within {Timeout}; the save to {ContainerName} goes ahead on the unique name key alone.",
                    lockName, LockTimeout, containerName);
            }

            var existing = await FileDescriptorRepository.FindByNameAsync(containerName, name, cancellationToken);
            if (existing != null && !overrideExisting)
            {
                throw new BusinessException(BlobManagementErrorCodes.BlobAlreadyExists)
                    .WithData("ContainerName", containerName)
                    .WithData("BlobName", name);
            }

            // The replaced descriptor gives up the address before the new one is inserted: the name key is unique, so
            // both cannot hold it at once. Until the new one is in, the name finds nobody - the lookup is by the key -
            // which is what the name lock keeps another save from seeing.
            if (existing != null)
            {
                existing.ReleaseNameKey();
                await FileDescriptorRepository.UpdateAsync(existing, autoSave: true, cancellationToken);
            }

            FileDescriptor descriptor;
            try
            {
                descriptor = await StoreAsync(
                    containerName, name, stream, clientId, nameIsAddress: true,
                    file =>
                    {
                        file.SetPublic(isPublic);
                        if (creatorId.HasValue)
                        {
                            file.CreatorId = creatorId;
                        }
                    },
                    cancellationToken);
            }
            catch
            {
                if (existing != null)
                {
                    await TryClaimNameKeyAsync(existing);
                }

                throw;
            }

            if (existing != null)
            {
                await DeleteAsync(existing, cancellationToken);
            }

            return descriptor;
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
    /// <paramref name="containerName"/>, under <paramref name="directoryId"/> (<c>null</c>: its top): how an
    /// application stores a file for its user. The name need not be unique: the file is addressed by its id, and holds no
    /// <see cref="FileDescriptor.NameKey"/>. Otherwise it is stored as <see cref="CreateAsync"/> stores (see
    /// <see cref="StoreAsync"/>). One difference: when the container stores the upload in another format (a JPEG as
    /// WebP), the name takes that format's extension (<see cref="GetNameForStoredType"/>).
    /// <para>
    /// The directory must be one of the container, which must allow directories
    /// (<see cref="BlobManagementErrorCodes.DirectoryNotFound"/>, <see cref="BlobManagementErrorCodes.DirectoriesNotAllowed"/>).
    /// The file is filed before it is inserted, so the row is written once.
    /// </para>
    /// </summary>
    /// <param name="configureFile">Runs on the new descriptor before it is inserted: a caller that keeps fields of its
    /// own on the file (extra properties) sets them here, so the row is written once. When it throws, the upload fails
    /// and its BLOB is deleted again.</param>
    /// <param name="clientId">The OAuth client writing through the file API, recorded on the descriptor.</param>
    /// <param name="id">The id the file takes; <c>null</c> generates one. An upload ticket names its file's id in
    /// advance, and its second use fails on the primary key - after its bytes were stored, which are removed again
    /// as for any insert that fails.</param>
    public virtual async Task<FileDescriptor> UploadAsync(
        string containerName,
        string name,
        Stream stream,
        Guid? directoryId = null,
        Action<FileDescriptor>? configureFile = null,
        string? clientId = null,
        Guid? id = null,
        CancellationToken cancellationToken = default)
    {
        GetBlobManagementContainerConfiguration(containerName);
        Check.NotNullOrWhiteSpace(name, nameof(name), FileDescriptorConsts.MaxNameLength);
        Check.NotNull(stream, nameof(stream));
        await UsageManager.CheckEnabledAsync();
        await CheckDirectoryAsync(containerName, directoryId, cancellationToken);

        Action<FileDescriptor> place = file =>
        {
            file.SetDirectory(directoryId);
            configureFile?.Invoke(file);
        };

        return await StoreAsync(
            containerName, name, stream, clientId, nameIsAddress: false, place, cancellationToken, id);
    }

    /// <summary>
    /// Deletes the descriptor (soft) in the current unit of work, then - once that unit of work has committed - its
    /// BLOB.
    /// <para>
    /// The descriptor first gives up its name key, so the deleted row does not keep the name from being saved again.
    /// The BLOB step is registered with <c>IUnitOfWork.OnCompleted</c>, which runs only after a successful commit: a
    /// rollback brings the row back with its bytes. Without a unit of work it runs at once.
    /// </para>
    /// <para>
    /// The step is best-effort: a failure is logged and leaves an unreferenced BLOB - unreachable bytes, never lost
    /// data - and does not surface to a request whose transaction already committed. ABP's BLOB containers cannot be
    /// enumerated, so reclaiming such orphans belongs to tooling on the concrete storage backend.
    /// </para>
    /// </summary>
    public virtual async Task DeleteAsync(FileDescriptor file, CancellationToken cancellationToken = default)
    {
        if (file.NameKey != null)
        {
            // Saved on its own: ABP's EF Core soft delete reloads the row before it marks it deleted, which would
            // discard a change made in the same save.
            file.ReleaseNameKey();
            await FileDescriptorRepository.UpdateAsync(file, autoSave: true, cancellationToken);
        }

        await FileDescriptorRepository.DeleteAsync(file, autoSave: true, cancellationToken);

        // Captured now: the step may run after the caller has moved on from this entity and this tenant.
        var tenantId = file.TenantId;
        var containerName = file.ContainerName;
        var blobName = file.BlobName;

        var unitOfWork = UnitOfWorkManager.Current;
        if (unitOfWork == null)
        {
            await DeleteBlobAsync(tenantId, containerName, blobName);
            return;
        }

        unitOfWork.OnCompleted(() => DeleteBlobAsync(tenantId, containerName, blobName));
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
    /// Renames an uploaded file, whose name is a display name and need not be unique. A file addressed by its name is
    /// not renamed: the name is how the application that saved it reads it back
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
    /// The live descriptor an application saved under <paramref name="name"/> in <paramref name="containerName"/>, or
    /// <c>null</c>.
    /// </summary>
    public virtual async Task<FileDescriptor?> FindAsync(
        string containerName,
        string name,
        CancellationToken cancellationToken = default)
    {
        GetBlobManagementContainerConfiguration(containerName);
        if (name.IsNullOrWhiteSpace())
        {
            return null;
        }

        return await FileDescriptorRepository.FindByNameAsync(containerName, name, cancellationToken);
    }

    /// <summary>
    /// The live, confirmed public file kept by id in <paramref name="containerName"/> whose physical BLOB is
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

    /// <summary>
    /// Buffers, detects and hashes the upload, saves it under a new BLOB name and writes its descriptor.
    /// <para>
    /// The upload is copied once into memory, capped at what the container's pipeline enforces (its <c>MaxSize</c>
    /// contributor, else <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/>), so an oversized upload fails
    /// with <c>Dignite.Abp.BlobStoring:ContentTooLarge</c> before anything is stored. Its type is detected from the
    /// bytes and reconciled with the extension of <paramref name="name"/>
    /// (<c>Dignite.Abp.BlobStoring:ContentTypeMismatch</c>), and it is hashed (SHA-256, upper-case hex).
    /// </para>
    /// <para>
    /// The tenant's allocation is checked next: content is refused when it would take the tenant past its storage size
    /// (<see cref="BlobManagementErrorCodes.StorageQuotaExceeded"/>, measured by the upload's length, see
    /// <see cref="BlobManagementUsageManager"/>).
    /// </para>
    /// <para>
    /// Then the BLOB is saved under a new name (<see cref="CreateBlobName"/>) without overwriting - the contributors run
    /// here and may reject the upload - and the descriptor is written with <c>autoSave</c>. A failed write - a unique
    /// key, the primary key, <paramref name="configureFile"/> throwing - deletes the BLOB again.
    /// </para>
    /// <para>
    /// A container may store content in another format than it was uploaded in - the images preset stores a JPEG as
    /// WebP. The physical BLOB is named for the stored type, which the container declares (<see cref="GetStoredMimeType"/>),
    /// and the descriptor records the type and size of the stored bytes; for a file addressed by its name,
    /// <paramref name="name"/> is kept.
    /// </para>
    /// </summary>
    /// <param name="nameIsAddress">Whether an application saves the file under its name, so that its descriptor holds the
    /// <see cref="FileDescriptor.NameKey"/>.</param>
    /// <param name="configureFile">Runs on the new descriptor before it is inserted (see <see cref="UploadAsync"/>).</param>
    /// <param name="id">The new descriptor's id; <c>null</c> generates one.</param>
    protected virtual async Task<FileDescriptor> StoreAsync(
        string containerName,
        string name,
        Stream stream,
        string? clientId,
        bool nameIsAddress,
        Action<FileDescriptor>? configureFile,
        CancellationToken cancellationToken,
        Guid? id = null)
    {
        var maxBytes = BlobStreamBuffering.GetMaxBufferedBytes(BlobContainerConfigurationProvider.Get(containerName));
        await using var content = await BlobStreamBuffering.CopyToBufferAsync(stream, maxBytes, cancellationToken);

        var mimeType = await MimeTypeDetector.DetectAsync(content, name, cancellationToken);
        var hash = await ComputeHashAsync(content, cancellationToken);

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
                id ?? GuidGenerator.Create(), containerName, storedName, blobName, storedType, size, hash,
                clientId, CurrentTenant.Id, nameIsAddress);
            configureFile?.Invoke(descriptor);

            // A unique-key violation (the name key, the primary key) fails here too, and the BLOB goes with it - as it
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
    /// The name of an uploaded file whose upload, detected as <paramref name="uploadedMimeType"/>, is stored as
    /// <paramref name="storedMimeType"/>: <paramref name="name"/> with the canonical extension of the stored type
    /// (<c>photo.jpg</c> stored as WebP becomes <c>photo.webp</c>), so a download is named for what it is.
    /// <paramref name="name"/> itself when the types are the same or the stored type has no extension in
    /// <see cref="BlobManagementFileTypes"/>. Not applied to a file an application saved, whose name is its address.
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
    /// to replace it. Best-effort, and the original exception is what the caller sees. In a transactional unit of
    /// work a failure here does not matter - the rollback gives the key back with everything else. Without a
    /// transaction it leaves the old file live but without its address: no longer found by its name, listed as a
    /// file kept by id, its bytes still counted - hence the warning, which names the file.
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
                "Blob Management could not give the name key back to the file {FileId} after an override failed.",
                existing.Id);
        }
    }

    /// <summary>
    /// The step of <see cref="DeleteAsync"/> that runs after commit: deletes the deleted descriptor's BLOB, in its
    /// tenant, on a bounded token of its own - the request's may be cancelled, or gone, by now.
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
                "Blob Management could not delete the BLOB {BlobName} in {ContainerName} after its descriptor was deleted; it is left in storage.",
                blobName, containerName);
        }
    }

    protected virtual string GetNameLockName(Guid? tenantId, string containerName, string name)
    {
        return NameLockName + FileDescriptor.CreateNameKey(tenantId, containerName, name);
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
    /// its <c>OnCompleted</c> handlers, or after a rollback - so a row it inserted is settled before anyone else holds
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
