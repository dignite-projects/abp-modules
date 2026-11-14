using System;

namespace Dignite.Abp.BlobManagement.Public;

public class BlobManagementPublicFileDto
{
    public string Name { get; set; } = default!;

    public string MimeType { get; set; } = default!;

    /// <summary>The stored size; a resized picture has its own.</summary>
    public long Size { get; set; }

    /// <summary>
    /// Changes whenever the content behind the address does (an overwrite stores new bytes under a new physical BLOB),
    /// so it is what <see cref="EntityTag"/> is made of.
    /// </summary>
    public string ContentId { get; set; } = default!;

    /// <summary>
    /// When the bytes behind the address were stored: the file's creation time, since a file's bytes never change - an
    /// overwrite stores a new file. The HTTP endpoints send it as <c>Last-Modified</c>.
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// The opaque value of the read's entity tag, without quotes: <see cref="ContentId"/>, plus <c>-{W}x{H}</c> when a
    /// size is asked, with <c>"</c> and <c>\</c> removed. The HTTP endpoint quotes it into the <c>ETag</c>.
    /// </summary>
    public string EntityTag { get; set; } = default!;
}
