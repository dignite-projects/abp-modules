using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.Auditing;
using Volo.Abp.BlobStoring;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Uow;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// A <see cref="FileDescriptorManager"/> that never gets the name lock, as when another holder keeps it past the
/// timeout. Registered by convention as itself only, so it does not replace the real manager.
/// </summary>
public class LocklessFileDescriptorManager : FileDescriptorManager
{
    public LocklessFileDescriptorManager(
        IFileDescriptorRepository fileDescriptorRepository,
        IDirectoryDescriptorRepository directoryRepository,
        IBlobContainerFactory blobContainerFactory,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider,
        IMimeTypeDetector mimeTypeDetector,
        BlobManagementUsageManager usageManager,
        IAbpDistributedLock distributedLock,
        IUnitOfWorkManager unitOfWorkManager,
        IAuditPropertySetter auditPropertySetter)
        : base(
            fileDescriptorRepository, directoryRepository, blobContainerFactory, blobContainerConfigurationProvider,
            mimeTypeDetector, usageManager, distributedLock, unitOfWorkManager, auditPropertySetter)
    {
    }

    protected override Task<IAbpDistributedLockHandle?> TryAcquireLockAsync(string lockName, CancellationToken cancellationToken)
    {
        return Task.FromResult<IAbpDistributedLockHandle?>(null);
    }
}
