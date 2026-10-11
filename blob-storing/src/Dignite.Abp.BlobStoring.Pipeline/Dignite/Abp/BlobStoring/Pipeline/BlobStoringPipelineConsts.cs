namespace Dignite.Abp.BlobStoring.Pipeline;

public static class BlobStoringPipelineConsts
{
    /// <summary>
    /// The cap applied when a contributor has to buffer a non-seekable stream to inspect it and the
    /// container has no <see cref="MaxSizeContributor"/> limit, so content is never buffered without a
    /// bound. Default: 100 MB. Containers that need more configure <c>AddMaxSizeContributor</c>.
    /// </summary>
    public static long DefaultMaxBufferedBytes { get; set; } = 100L * 1024 * 1024;
}
