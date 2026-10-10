namespace Dignite.Abp.FileStoring;

public static class BlobContainerConfigurationNames
{
    public const string FileHandlers = "FileHandlers";

    /// <summary>
    /// The <see cref="IBlobNameGenerator"/> type <see cref="IFileStorer"/> uses for the container.
    /// </summary>
    public const string BlobNameGenerator = "BlobNameGenerator";
}
