using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.Auditing;
using Volo.Abp.BlobStoring;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Uow;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// A <see cref="FileDescriptorManager"/> that treats every unit of work as one whose failure rolls its rows back, as a
/// transactional one does: the test databases run without transactions. Registered by convention as itself only, so it
/// does not replace the real manager.
/// </summary>
public class RollingBackFileDescriptorManager : FileDescriptorManager
{
    public RollingBackFileDescriptorManager(
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

    protected override bool IsRolledBackOnFailure(IUnitOfWork unitOfWork)
    {
        return true;
    }
}
