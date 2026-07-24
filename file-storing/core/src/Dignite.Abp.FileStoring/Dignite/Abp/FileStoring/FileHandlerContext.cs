using System.IO;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.FileStoring;

public class FileHandlerContext
{
    public FileHandlerContext(
        string fileName,
        string mimeType,
        Stream blobStream,
        BlobContainerConfiguration containerConfiguration)
    {
        FileName = fileName;
        MimeType = mimeType;
        BlobStream = blobStream;
        ContainerConfiguration = containerConfiguration;
    }

    public string FileName { get; }

    public string MimeType { get; }

    public Stream BlobStream { get; set; }

    public BlobContainerConfiguration ContainerConfiguration { get; }
}
