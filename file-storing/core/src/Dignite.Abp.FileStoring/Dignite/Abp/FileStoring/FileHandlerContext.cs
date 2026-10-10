using System.IO;
using System.Threading;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.FileStoring;

/// <summary>
/// What an <see cref="IFileHandler"/> works on. Validators inspect <see cref="BlobStream"/>; transforms
/// replace it. <see cref="IFileStorer"/> owns (and disposes) every stream assigned here, so a handler
/// must leave the stream it received open when it replaces it.
/// </summary>
public class FileHandlerContext
{
    public FileHandlerContext(
        string fileName,
        string mimeType,
        Stream blobStream,
        BlobContainerConfiguration containerConfiguration,
        CancellationToken cancellationToken = default)
    {
        FileName = fileName;
        MimeType = mimeType;
        BlobStream = blobStream;
        ContainerConfiguration = containerConfiguration;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// The client's file name. Its extension has already been checked against the content when the
    /// handler runs under <see cref="IFileStorer"/>.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// The MIME type detected from the content by <see cref="IMimeTypeDetector"/> when the handler runs
    /// under <see cref="IFileStorer"/> — not the uploader's claim.
    /// </summary>
    public string MimeType { get; }

    public Stream BlobStream { get; set; }

    public BlobContainerConfiguration ContainerConfiguration { get; }

    /// <summary>
    /// The cancellation token of the store operation. Pass it to any I/O or decoding the handler does.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
