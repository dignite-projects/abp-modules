using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace Dignite.Abp.BlobManagement.Uploads;

/// <summary>
/// Deletes the files uploaded through a ticket that their business system never confirmed: pending for longer than
/// <see cref="BlobManagementUploadOptions.PendingLifetime"/>. A browser that uploads and leaves, or a record that was
/// never saved, would otherwise keep bytes the tenant pays for. Every tenant is looked at - the host, then each tenant
/// <see cref="ITenantStore"/> lists, in its own database if it has one - and each file is deleted as any other is
/// (<see cref="FileDescriptorManager.DeleteAsync"/>), its BLOB after its last descriptor.
/// <para>
/// Several Blob Management instances run the cleanup each on its own schedule; a distributed lock lets one of them
/// work at a time, and an instance that finds it taken skips its turn.
/// </para>
/// </summary>
public class BlobManagementPendingFileCleaner : DomainService
{
    public const string LockName = "Dignite.Abp.BlobManagement.PendingFileCleanup";

    /// <summary>How many files one unit of work deletes.</summary>
    protected const int BatchSize = 100;

    protected IFileDescriptorRepository FileRepository { get; }

    protected FileDescriptorManager FileDescriptorManager { get; }

    protected ITenantStore TenantStore { get; }

    protected IAbpDistributedLock DistributedLock { get; }

    protected IUnitOfWorkManager UnitOfWorkManager { get; }

    protected BlobManagementUploadOptions Options { get; }

    public BlobManagementPendingFileCleaner(
        IFileDescriptorRepository fileRepository,
        FileDescriptorManager fileDescriptorManager,
        ITenantStore tenantStore,
        IAbpDistributedLock distributedLock,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<BlobManagementUploadOptions> options)
    {
        FileRepository = fileRepository;
        FileDescriptorManager = fileDescriptorManager;
        TenantStore = tenantStore;
        DistributedLock = distributedLock;
        UnitOfWorkManager = unitOfWorkManager;
        Options = options.Value;
    }

    /// <summary>
    /// Deletes every file still pending that was uploaded more than
    /// <see cref="BlobManagementUploadOptions.PendingLifetime"/> ago, in every tenant; how many it deleted. Nothing,
    /// when another instance holds the lock.
    /// </summary>
    public virtual Task<int> CleanAsync(CancellationToken cancellationToken = default)
    {
        return CleanAsync(Clock.Now - Options.PendingLifetime, cancellationToken);
    }

    /// <summary>
    /// Deletes every file still pending that was uploaded before <paramref name="createdBefore"/>, in every tenant; how
    /// many it deleted. Nothing, when another instance holds the lock.
    /// </summary>
    public virtual async Task<int> CleanAsync(DateTime createdBefore, CancellationToken cancellationToken = default)
    {
        await using var handle = await DistributedLock.TryAcquireAsync(LockName, TimeSpan.Zero, cancellationToken);
        if (handle == null)
        {
            Logger.LogDebug("Another instance is cleaning up the pending files of Blob Management; this one skips its turn.");
            return 0;
        }

        var deleted = await CleanAsync(null, createdBefore, cancellationToken);
        foreach (var tenant in await TenantStore.GetListAsync())
        {
            deleted += await CleanAsync(tenant.Id, createdBefore, cancellationToken);
        }

        if (deleted > 0)
        {
            Logger.LogInformation("Blob Management deleted {Count} files that were never confirmed.", deleted);
        }

        return deleted;
    }

    /// <summary>The cleanup of one tenant (<c>null</c>: the host), a batch per unit of work.</summary>
    protected virtual async Task<int> CleanAsync(Guid? tenantId, DateTime createdBefore, CancellationToken cancellationToken)
    {
        var deleted = 0;
        using (CurrentTenant.Change(tenantId))
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int count;
                using (var unitOfWork = UnitOfWorkManager.Begin(requiresNew: true))
                {
                    var files = await FileRepository.GetExpiredPendingListAsync(createdBefore, BatchSize, cancellationToken);
                    foreach (var file in files)
                    {
                        await FileDescriptorManager.DeleteAsync(file, cancellationToken);
                    }

                    await unitOfWork.CompleteAsync(cancellationToken);
                    count = files.Count;
                }

                deleted += count;
                if (count < BatchSize)
                {
                    break;
                }
            }
        }

        return deleted;
    }
}
