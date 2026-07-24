using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Collections;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Services;
using Volo.Abp.Threading;

namespace Dignite.FileExplorer.Files;

public class FileDescriptorManager : DomainService
{
    private readonly IFileDescriptorRepository _fileDescriptorRepository;
    private readonly IBlobContainerFactory _blobContainerFactory;
    private readonly IBlobContainerConfigurationProvider _blobContainerConfigurationProvider;
    private readonly ContainerNameValidator _containerNameValidator;

    private const int CompensationTimeoutSeconds = 30;

    // Backstop cap for containers that do not configure a size limit, so a single upload
    // cannot buffer unbounded memory. Containers needing larger uploads set their own MaxFileSize.
    private const long DefaultMaxFileSizeInBytes = 100L * 1024 * 1024;

    public FileDescriptorManager(
        IFileDescriptorRepository fileDescriptorRepository,
        IBlobContainerFactory blobContainerFactory,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider,
        ContainerNameValidator containerNameValidator)
    {
        _fileDescriptorRepository = fileDescriptorRepository;
        _blobContainerFactory = blobContainerFactory;
        _blobContainerConfigurationProvider = blobContainerConfigurationProvider;
        _containerNameValidator = containerNameValidator;
    }

    public virtual async Task<FileDescriptor> CreateAsync<TContainer>(
        [NotNull] IRemoteStreamContent stream,
        [CanBeNull] string cellName,
        [CanBeNull] Guid? directoryId,
        [CanBeNull] IEntity entity,
        CancellationToken cancellationToken = default)
        where TContainer : class
    {
        var containerName = BlobContainerNameAttribute.GetContainerName<TContainer>();
        return await CreateAsync(containerName, stream, cellName, directoryId, entity, cancellationToken);
    }

    public virtual async Task<FileDescriptor> CreateAsync(
        [NotNull] string containerName,
        [NotNull] IRemoteStreamContent stream,
        [CanBeNull] string cellName,
        [CanBeNull] Guid? directoryId,
        [CanBeNull] IEntity entity,
        CancellationToken cancellationToken = default)
    {
        return await CreateAsync(
            containerName,
            stream,
            cellName,
            directoryId,
            entity == null ? null : GetEntityKey(entity),
            cancellationToken
        );
    }

    public virtual async Task<FileDescriptor> CreateAsync(
        [NotNull] string containerName,
        [NotNull] IRemoteStreamContent stream,
        [CanBeNull] string cellName,
        [CanBeNull] Guid? directoryId,
        [CanBeNull] string entityId,
        CancellationToken cancellationToken = default)
    {
        ValidateFileCell(containerName, cellName);

        var blobName = await GenerateBlobNameAsync(containerName);
        var fileDescriptor = new FileDescriptor(
            GuidGenerator.Create(),
            containerName,
            blobName,
            stream.FileName,
            stream.ContentType,
            cellName,
            directoryId,
            entityId,
            CurrentTenant.Id);

        return await CreateAsync(fileDescriptor, stream, cancellationToken);
    }

    public virtual async Task<FileDescriptor> CreateAsync(
        [NotNull] FileDescriptor file,
        [NotNull] IRemoteStreamContent remoteStream,
        CancellationToken cancellationToken = default)
    {
        var maxFileSizeInBytes = _blobContainerConfigurationProvider
            .Get(file.ContainerName)
            .GetFileSizeLimitConfiguration()
            .MaxFileSizeInBytes;

        using (var ms = new MemoryStream())
        {
            await CopyToAsync(remoteStream.GetStream(), ms, maxFileSizeInBytes, cancellationToken);
            ms.Position = 0;
            return await CreateAsync(file, ms, true, cancellationToken);
        }
    }

    public virtual async Task<FileDescriptor> CreateAsync(
        [NotNull] FileDescriptor file,
        [NotNull] Stream stream,
        bool overrideExisting = false,
        CancellationToken cancellationToken = default)
    {
        await ValidateAsync(file);

        await OnCreatingEntityAsync(file);

        var existingFile = overrideExisting
            ? await _fileDescriptorRepository.FindByBlobNameAsync(file.ContainerName, file.BlobName, cancellationToken)
            : null;

        if (!overrideExisting && await _fileDescriptorRepository.BlobNameExistsAsync(file.ContainerName, file.BlobName, cancellationToken))
        {
            throw new BlobAlreadyExistsException(
                $"Saving BLOB '{file.BlobName}' does already exists in the container '{file.ContainerName}'! Set {nameof(overrideExisting)} if it should be overwritten.");
        }

        var previousFile = existingFile == null ? null : CopyFileDescriptor(existingFile);
        var previousBlobName = existingFile == null ? null : GetActualBlobName(existingFile);
        MemoryStream previousBlob = null;
        var metadataDeleteAttempted = false;
        var metadataDeleted = false;
        var metadataInserted = false;
        var blobSaveAttempted = false;

        try
        {
            if (previousBlobName != null)
            {
                previousBlob = await BackupBlobAsync(file.ContainerName, previousBlobName, cancellationToken);
            }

            stream = await FileHandlers(file, stream, cancellationToken);
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            file = await PrepareFileInformationAsync(file, stream, cancellationToken);

            if (file.ReferBlobName.IsNullOrEmpty())
            {
                var blobContainer = _blobContainerFactory.Create(file.ContainerName);
                blobSaveAttempted = true;
                await blobContainer.SaveAsync(file.BlobName, stream, true, cancellationToken);
            }

            if (existingFile != null)
            {
                metadataDeleteAttempted = true;
                await _fileDescriptorRepository.DeleteAsync(existingFile, true, cancellationToken);
                metadataDeleted = true;
            }

            await _fileDescriptorRepository.InsertAsync(file, true, cancellationToken);
            metadataInserted = true;

            await OnCreatedEntityAsync(file);

            if (previousBlobName != null && previousBlobName != GetActualBlobName(file))
            {
                await DeleteBlobIfUnusedAsync(file.ContainerName, previousBlobName, cancellationToken);
            }

            return file;
        }
        catch
        {
            // Compensation must still run when the request was cancelled. Reusing the
            // request token here makes every cleanup operation cancel immediately and
            // leaves the newly written blob or the previous version behind. Use an
            // independent, bounded token so cancellation remains responsive without
            // sacrificing storage consistency.
            using var compensationCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(CompensationTimeoutSeconds));
            var compensationToken = compensationCts.Token;

            if (metadataInserted)
            {
                await TryDeleteMetadataAsync(file, compensationToken);
            }

            if (blobSaveAttempted)
            {
                await TryDeleteBlobAsync(file.ContainerName, file.BlobName, compensationToken);
            }

            if ((metadataDeleteAttempted || metadataDeleted) && previousFile != null)
            {
                await TryRestoreMetadataAsync(previousFile, compensationToken);
            }

            if (previousBlob != null && previousBlobName != null)
            {
                await TryRestoreBlobAsync(file.ContainerName, previousBlobName, previousBlob, compensationToken);
            }

            throw;
        }
        finally
        {
            previousBlob?.Dispose();
        }
    }

    public virtual async Task ValidateAsync([NotNull] FileDescriptor file)
    {
        _containerNameValidator.Validate(file.ContainerName);
        ValidateFileCell(file.ContainerName, file.CellName);
        await CheckFileAsync(file);
    }

    public virtual async Task<FileDescriptor> GetOrNullAsync(
        [NotNull] string containerName,
        [NotNull] string blobName,
        CancellationToken cancellationToken = default)
    {
        return await _fileDescriptorRepository.FindByBlobNameAsync(containerName, blobName, cancellationToken);
    }

    public virtual async Task<FileDescriptor> GetOrNullAsync<TContainer>(
        [NotNull] string blobName,
        CancellationToken cancellationToken = default)
        where TContainer : class
    {
        var containerName = BlobContainerNameAttribute.GetContainerName<TContainer>();
        return await GetOrNullAsync(containerName, blobName, cancellationToken);
    }

    public virtual async Task<Stream> GetStreamOrNullAsync(
        [NotNull] string containerName,
        [NotNull] string blobName,
        CancellationToken cancellationToken = default)
    {
        var blobContainer = _blobContainerFactory.Create(containerName);

        return await blobContainer.GetOrNullAsync(blobName, cancellationToken);
    }

    public virtual async Task<Stream> GetStreamOrNullAsync<TContainer>(
        [NotNull] string blobName,
        CancellationToken cancellationToken = default)
        where TContainer : class
    {
        var blobContainer = _blobContainerFactory.Create<TContainer>();

        return await blobContainer.GetOrNullAsync(blobName, cancellationToken);
    }

    public virtual async Task<bool> DeleteAsync(
        [NotNull] FileDescriptor file,
        CancellationToken cancellationToken = default)
    {
        await OnDeletingEntityAsync(file);

        // autoSave: true - commit the metadata deletion before checking for remaining
        // references. Otherwise this file's own row is still visible to the queries below
        // (nothing has been flushed to the database yet) and it ends up counting itself as
        // a reference, so the physical blob is never deleted even when this is the last one.
        await _fileDescriptorRepository.DeleteAsync(file, true, cancellationToken);

        var blobName = GetActualBlobName(file);
        var isReference = !file.ReferBlobName.IsNullOrEmpty();

        var stillReferenced = await _fileDescriptorRepository.ReferencingAnyAsync(file.ContainerName, blobName, cancellationToken);
        var stillOwned = isReference && await _fileDescriptorRepository.BlobNameExistsAsync(file.ContainerName, blobName, cancellationToken);

        // Known limitation (orphan blobs): metadata is committed before the blob is deleted here, and
        // the create path writes the blob before committing metadata. Caught failures are compensated,
        // but a hard process crash (kill/OOM/power loss) in the window between the two writes can leave
        // an orphan blob (unreferenced bytes) — never lost data, and the reference checks above ensure a
        // still-referenced blob is never deleted. There is no background sweep to reclaim such orphans:
        // ABP's IBlobContainer exposes no list/enumerate API, so blobs cannot be scanned against
        // metadata, and a persistent pending-delete queue would cross this repo's "no new aggregate /
        // no outbox" invariant. Reclaiming orphans, if ever needed, belongs to deployment-side tooling
        // against the concrete storage backend.
        var blobDeleted = true;
        if (!stillReferenced && !stillOwned)
        {
            var blobContainer = _blobContainerFactory.Create(file.ContainerName);
            blobDeleted = await blobContainer.DeleteAsync(blobName, cancellationToken);
        }

        await OnDeletedEntityAsync(file);

        return blobDeleted;
    }

    protected virtual Task OnCreatingEntityAsync([NotNull] FileDescriptor file)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnCreatedEntityAsync([NotNull] FileDescriptor file)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnDeletingEntityAsync([NotNull] FileDescriptor file)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnDeletedEntityAsync([NotNull] FileDescriptor file)
    {
        return Task.CompletedTask;
    }

    protected virtual Task CheckFileAsync([NotNull] FileDescriptor file)
    {
        Check.NotNullOrWhiteSpace(file.ContainerName, nameof(FileDescriptor.ContainerName), FileConsts.MaxContainerNameLength);
        Check.NotNullOrWhiteSpace(file.BlobName, nameof(FileDescriptor.BlobName), FileConsts.MaxBlobNameLength);
        Check.Length(file.Name, nameof(FileDescriptor.Name), FileConsts.MaxNameLength);
        Check.Length(file.MimeType, nameof(FileDescriptor.MimeType), FileConsts.MaxMimeTypeLength);

        return Task.CompletedTask;
    }

    private void ValidateFileCell(string containerName, string cellName)
    {
        var fileCells = _blobContainerConfigurationProvider
            .Get(containerName)
            .GetFileGridConfiguration()
            .FileCells;

        if ((fileCells == null || !fileCells.Any()) && !cellName.IsNullOrEmpty())
        {
            throw new FileCellNameNotApplicableException();
        }

        if (fileCells != null && fileCells.Any())
        {
            if (cellName.IsNullOrEmpty())
            {
                throw new ArgumentNullException(nameof(cellName));
            }

            if (!fileCells.Any(c => c.Name.Equals(cellName, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw new FileCellNameNotFoundException();
            }
        }
    }

    private static async Task CopyToAsync(
        Stream source,
        Stream destination,
        long maxFileSizeInBytes,
        CancellationToken cancellationToken)
    {
        // A container without an explicit size limit still gets a backstop cap rather than
        // an unbounded copy into memory.
        var effectiveLimit = maxFileSizeInBytes > 0 ? maxFileSizeInBytes : DefaultMaxFileSizeInBytes;

        var buffer = new byte[81920];
        long totalBytes = 0;
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > effectiveLimit)
            {
                var maxFileSizeInMegabytes = effectiveLimit / (1024 * 1024);
                throw new BusinessException(
                    code: FileErrorCodes.Files.FileTooLarge,
                    message: "File object is too large",
                    details: $"The file object size cannot exceed {maxFileSizeInMegabytes} MB!"
                );
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    protected virtual string GetEntityKey(IEntity entity)
    {
        var keys = entity.GetKeys();
        if (keys.All(k => k == null))
        {
            return null;
        }

        return string.Join(",", keys);
    }

    private async Task<Stream> FileHandlers(
        FileDescriptor file,
        Stream stream,
        CancellationToken cancellationToken)
    {
        var configuration = _blobContainerConfigurationProvider.Get(file.ContainerName);
        var processHandlers = configuration.GetConfigurationOrDefault<ITypeList<IFileHandler>>(BlobContainerConfigurationNames.FileHandlers, null);
        if (processHandlers != null && processHandlers.Any())
        {
            var serviceProvider = LazyServiceProvider.LazyGetRequiredService<IServiceProvider>();
            var context = new FileHandlerContext(file.Name, file.MimeType, stream, configuration);
            using (var scope = serviceProvider.CreateScope())
            {
                foreach (var handlerType in processHandlers)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var handler = scope.ServiceProvider
                        .GetRequiredService(handlerType)
                        .As<IFileHandler>();

                    await handler.ExecuteAsync(context);
                }

                return context.BlobStream;
            }
        }

        return stream;
    }

    private async Task<FileDescriptor> PrepareFileInformationAsync(
        [NotNull] FileDescriptor file,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        // Content-dedup decision. This is a check-then-act against FindByMd5Async, so it is not
        // race-free: two concurrent uploads of identical content in the same (tenant, container)
        // can both miss here and both proceed to store. The (TenantId, ContainerName, Md5) filtered
        // unique index is the real arbiter — the loser's InsertAsync then violates it, CreateAsync's
        // catch compensates (removes the just-written blob/row), and it surfaces as a retriable error
        // (a retry re-runs this method, now finds the winner, and dedups to a reference).
        // Known limitation: the race loser is NOT converted to a reference in-request. Doing that
        // safely needs a fresh unit of work for the retry (an EF DbContext is not reliably reusable
        // after a failed SaveChanges); the current behavior is safe (no corruption, no orphan) but
        // not graceful under a true simultaneous collision.
        var contentHash = stream.Sha256();
        var hashExistingFile = await _fileDescriptorRepository.FindByMd5Async(file.ContainerName, contentHash, cancellationToken);
        if (hashExistingFile == null || hashExistingFile.BlobName == file.BlobName)
        {
            file.SetSize(stream.Length);
            file.SetMd5(contentHash);
            file.SetReferBlobName(string.Empty);
        }
        else
        {
            file.SetSize(hashExistingFile.Size);
            file.SetReferBlobName(GetActualBlobName(hashExistingFile));
        }

        return file;
    }

    private async Task<MemoryStream> BackupBlobAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken)
    {
        var blob = await _blobContainerFactory.Create(containerName).GetOrNullAsync(blobName, cancellationToken);
        if (blob == null)
        {
            return null;
        }

        using (blob)
        {
            var backup = new MemoryStream();
            await blob.CopyToAsync(backup, cancellationToken);
            backup.Position = 0;
            return backup;
        }
    }

    private async Task DeleteBlobIfUnusedAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken)
    {
        if (await _fileDescriptorRepository.ReferencingAnyAsync(containerName, blobName, cancellationToken) ||
            await _fileDescriptorRepository.FindByBlobNameAsync(containerName, blobName, cancellationToken) != null)
        {
            return;
        }

        await _blobContainerFactory.Create(containerName).DeleteAsync(blobName, cancellationToken);
    }

    private async Task TryDeleteMetadataAsync(FileDescriptor file, CancellationToken cancellationToken)
    {
        try
        {
            await _fileDescriptorRepository.DeleteAsync(file, true, cancellationToken);
        }
        catch
        {
            // Best-effort compensation must not hide the original exception.
        }
    }

    private async Task TryDeleteBlobAsync(string containerName, string blobName, CancellationToken cancellationToken)
    {
        try
        {
            await _blobContainerFactory.Create(containerName).DeleteAsync(blobName, cancellationToken);
        }
        catch
        {
            // Best-effort compensation must not hide the original exception.
        }
    }

    private async Task TryRestoreMetadataAsync(FileDescriptor file, CancellationToken cancellationToken)
    {
        try
        {
            await _fileDescriptorRepository.InsertAsync(file, true, cancellationToken);
        }
        catch
        {
            // Best-effort compensation must not hide the original exception.
        }
    }

    private async Task TryRestoreBlobAsync(
        string containerName,
        string blobName,
        MemoryStream backup,
        CancellationToken cancellationToken)
    {
        try
        {
            backup.Position = 0;
            await _blobContainerFactory.Create(containerName).SaveAsync(blobName, backup, true, cancellationToken);
        }
        catch
        {
            // Best-effort compensation must not hide the original exception.
        }
    }

    private static string GetActualBlobName(FileDescriptor file)
    {
        return file.ReferBlobName.IsNullOrEmpty() ? file.BlobName : file.ReferBlobName;
    }

    private static FileDescriptor CopyFileDescriptor(FileDescriptor file)
    {
        var copy = new FileDescriptor(
            file.Id,
            file.ContainerName,
            file.BlobName,
            file.Name,
            file.MimeType,
            file.CellName,
            file.DirectoryId,
            file.EntityId,
            file.TenantId);

        copy.SetSize(file.Size);
        copy.SetMd5(file.Md5);
        copy.SetReferBlobName(file.ReferBlobName);
        copy.CreationTime = file.CreationTime;
        copy.CreatorId = file.CreatorId;
        copy.DeleterId = file.DeleterId;
        copy.DeletionTime = file.DeletionTime;
        copy.IsDeleted = file.IsDeleted;
        return copy;
    }

    private async Task<string> GenerateBlobNameAsync(string containerName)
    {
        var configuration = _blobContainerConfigurationProvider.Get(containerName);
        var namingGeneratorType = configuration.GetConfigurationOrDefault(
            FileExplorerBlobContainerConfigurationNames.BlobNamingGenerator,
            typeof(RandomBlobNameGenerator)
        );

        var generator = LazyServiceProvider.LazyGetRequiredService(namingGeneratorType)
            .As<IBlobNameGenerator>();

        return await generator.Create();
    }
}
