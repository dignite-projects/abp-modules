namespace Dignite.Abp.BlobStoring.Pipeline;

public static class BlobStoringPipelineConsts
{
    /// <summary>
    /// The cap applied when a contributor has to buffer a non-seekable stream to inspect it and the
    /// container has no <see cref="MaxSizeContributor"/> limit, so content is never buffered without a
    /// bound. Default: 100 MB. Containers that need more configure <c>AddMaxSizeContributor</c>.
    /// </summary>
    public static long DefaultMaxBufferedBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>
    /// How much content a buffered copy (<see cref="BlobStreamBuffering.CopyToSeekableAsync"/>) keeps in memory; beyond
    /// it the copy is a temporary file. Default: 64 KB, ASP.NET Core's own threshold for buffering a form's file
    /// (<c>FormOptions.MemoryBufferThreshold</c>). It does not raise or lower the cap a copy is bounded by.
    /// </summary>
    public static int MemoryBufferThreshold { get; set; } = 64 * 1024;
}
