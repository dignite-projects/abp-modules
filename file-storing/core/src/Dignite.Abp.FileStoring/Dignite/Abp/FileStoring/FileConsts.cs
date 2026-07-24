namespace Dignite.Abp.FileStoring;

public static class FileConsts
{
    public static int MaxContainerNameLength { get; set; } = 64;

    public static int MaxBlobNameLength { get; set; } = 256;

    public static int MaxNameLength { get; set; } = 128;

    public static int MaxMimeTypeLength { get; set; } = 128;

    public static int MaxMd5Length { get; set; } = 64;
}
