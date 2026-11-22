using System.Collections.Generic;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.Auditing;
using Volo.Abp.BlobStoring;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Uow;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// A <see cref="FileDescriptorManager"/> that records the BLOB names it generates, so a test can see whether a BLOB
/// whose descriptor could not be written was removed again. Registered by convention as itself only, so it does not
/// replace the real manager.
/// </summary>
public class RecordingFileDescriptorManager : FileDescriptorManager
{
    public RecordingFileDescriptorManager(
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

    public List<string> GeneratedBlobNames { get; } = new();

    protected override string CreateBlobName(string mimeType)
    {
        var blobName = base.CreateBlobName(mimeType);
        GeneratedBlobNames.Add(blobName);
        return blobName;
    }
}
