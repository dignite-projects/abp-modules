using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.BlobStoring;

namespace Dignite.Drive;

/// <summary>
/// A <see cref="FileDescriptorManager"/> whose dedup lookup always misses, as the second of two simultaneous uploads
/// of the same bytes would: both looked before either wrote. It records the BLOB names it generates, so a test can
/// see whether the loser's BLOB was removed again. Registered by convention as itself only, so it does not replace
/// the real manager.
/// </summary>
public class RacingFileDescriptorManager : FileDescriptorManager
{
    public RacingFileDescriptorManager(
        IFileDescriptorRepository fileDescriptorRepository,
        IBlobContainerFactory blobContainerFactory,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider,
        IMimeTypeDetector mimeTypeDetector,
        DriveUsageManager usageManager)
        : base(fileDescriptorRepository, blobContainerFactory, blobContainerConfigurationProvider, mimeTypeDetector, usageManager)
    {
    }

    public List<string> GeneratedBlobNames { get; } = new();

    protected override Task<FileDescriptor?> FindDedupSourceAsync(string containerName, string hash, CancellationToken cancellationToken)
    {
        return Task.FromResult<FileDescriptor?>(null);
    }

    protected override string CreateBlobName(string mimeType)
    {
        var blobName = base.CreateBlobName(mimeType);
        GeneratedBlobNames.Add(blobName);
        return blobName;
    }
}
