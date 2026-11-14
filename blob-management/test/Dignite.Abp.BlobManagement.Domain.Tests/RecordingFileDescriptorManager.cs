using System.Collections.Generic;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// A <see cref="FileDescriptorManager"/> that records the BLOB names it generates, so a test can see whether a BLOB
/// whose descriptor could not be inserted was removed again. Registered by convention as itself only, so it does not
/// replace the real manager.
/// </summary>
public class RecordingFileDescriptorManager : FileDescriptorManager
{
    public RecordingFileDescriptorManager(
        IFileDescriptorRepository fileDescriptorRepository,
        IBlobContainerFactory blobContainerFactory,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider,
        IMimeTypeDetector mimeTypeDetector,
        BlobManagementUsageManager usageManager)
        : base(fileDescriptorRepository, blobContainerFactory, blobContainerConfigurationProvider, mimeTypeDetector, usageManager)
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
