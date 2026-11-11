using Volo.Abp.Content;

namespace Dignite.Drive.Public;

/// <summary>
/// One public read, answered from one lookup of the file: <see cref="File"/>, <see cref="NotModified"/> and
/// <see cref="Content"/> all describe the same file, even when it is overwritten while the request runs.
/// </summary>
public class DrivePublicReadResult
{
    /// <summary>The file behind the address, including the <see cref="DrivePublicFileDto.EntityTag"/> of this read.</summary>
    public DrivePublicFileDto File { get; set; } = default!;

    /// <summary>One of the client's entity tags matches <see cref="DrivePublicFileDto.EntityTag"/>.</summary>
    public bool NotModified { get; set; }

    /// <summary>
    /// The bytes - the picture resized when a size was asked - or <c>null</c> when the read is
    /// <see cref="NotModified"/> or <see cref="DrivePublicReadRequest.HeadersOnly"/>, in which case nothing was read
    /// from storage. The caller disposes it.
    /// </summary>
    public IRemoteStreamContent? Content { get; set; }
}
