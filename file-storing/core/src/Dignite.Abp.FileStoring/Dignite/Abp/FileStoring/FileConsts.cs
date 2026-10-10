namespace Dignite.Abp.FileStoring;

public static class FileConsts
{
    public static int MaxContainerNameLength { get; set; } = 64;

    public static int MaxBlobNameLength { get; set; } = 256;

    public static int MaxNameLength { get; set; } = 128;

    public static int MaxMimeTypeLength { get; set; } = 128;

    public static int MaxMd5Length { get; set; } = 64;

    /// <summary>
    /// The size cap <see cref="IFileStorer"/> applies to a container that has no
    /// <c>AddFileSizeLimitHandler</c> limit, so no upload is ever buffered without a bound.
    /// Containers needing larger uploads configure their own limit.
    /// </summary>
    public static long DefaultMaxFileSizeInBytes { get; set; } = 100L * 1024 * 1024;
}
