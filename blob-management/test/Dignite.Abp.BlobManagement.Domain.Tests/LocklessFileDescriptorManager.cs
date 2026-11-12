using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.BlobStoring;
using Volo.Abp.DistributedLocking;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// A <see cref="FileDescriptorManager"/> that never gets the dedup lock, as when another holder keeps it past the
/// timeout. Registered by convention as itself only, so it does not replace the real manager.
/// </summary>
public class LocklessFileDescriptorManager : FileDescriptorManager
{
    public LocklessFileDescriptorManager(
        IFileDescriptorRepository fileDescriptorRepository,
        IBlobContainerFactory blobContainerFactory,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider,
        IMimeTypeDetector mimeTypeDetector,
        BlobManagementUsageManager usageManager)
        : base(fileDescriptorRepository, blobContainerFactory, blobContainerConfigurationProvider, mimeTypeDetector, usageManager)
    {
    }

    protected override Task<IAbpDistributedLockHandle?> TryAcquireDedupLockAsync(string lockName, CancellationToken cancellationToken)
    {
        return Task.FromResult<IAbpDistributedLockHandle?>(null);
    }
}
