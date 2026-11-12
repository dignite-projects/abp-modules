using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Public;

/// <summary>
/// One public read, answered from one lookup of the file: <see cref="File"/>, <see cref="NotModified"/> and
/// <see cref="Content"/> all describe the same file, even when it is overwritten while the request runs.
/// </summary>
public class BlobManagementPublicReadResult
{
    /// <summary>The file behind the address, including the <see cref="BlobManagementPublicFileDto.EntityTag"/> of this read.</summary>
    public BlobManagementPublicFileDto File { get; set; } = default!;

    /// <summary>One of the client's entity tags matches <see cref="BlobManagementPublicFileDto.EntityTag"/>.</summary>
    public bool NotModified { get; set; }

    /// <summary>
    /// The bytes - the picture resized when a size was asked - or <c>null</c> when the read is
    /// <see cref="NotModified"/> or <see cref="BlobManagementPublicReadRequest.HeadersOnly"/>, in which case nothing was read
    /// from storage. The caller disposes it.
    /// </summary>
    public IRemoteStreamContent? Content { get; set; }
}
