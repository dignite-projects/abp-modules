using JetBrains.Annotations;
using Volo.Abp;

namespace Dignite.Abp.FileStoring;

/// <summary>
/// What <see cref="IFileStorer.StoreAsync"/> stored. Every value describes the bytes that were
/// written to the blob container, i.e. after the handler pipeline ran (a resized image reports its
/// resized size and hash).
/// </summary>
public class StoredFileInfo
{
    /// <summary>
    /// The generated name of the blob in its container.
    /// </summary>
    [NotNull]
    public string BlobName { get; }

    /// <summary>
    /// The stored size in bytes.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// The MIME type detected from the stored content (see <see cref="IMimeTypeDetector"/>);
    /// never a value supplied by the uploader.
    /// </summary>
    [NotNull]
    public string MimeType { get; }

    /// <summary>
    /// The SHA-256 of the stored content as 64 upper-case hexadecimal characters.
    /// </summary>
    [NotNull]
    public string Hash { get; }

    public StoredFileInfo(
        [NotNull] string blobName,
        long size,
        [NotNull] string mimeType,
        [NotNull] string hash)
    {
        BlobName = Check.NotNullOrWhiteSpace(blobName, nameof(blobName));
        Size = size;
        MimeType = Check.NotNullOrWhiteSpace(mimeType, nameof(mimeType));
        Hash = Check.NotNullOrWhiteSpace(hash, nameof(hash));
    }
}
