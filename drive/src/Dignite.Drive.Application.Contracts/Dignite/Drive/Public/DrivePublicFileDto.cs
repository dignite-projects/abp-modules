namespace Dignite.Drive.Public;

public class DrivePublicFileDto
{
    public string Name { get; set; } = default!;

    public string MimeType { get; set; } = default!;

    public long Size { get; set; }

    /// <summary>
    /// Changes whenever the content behind the address does (an overwrite stores new bytes under a new physical BLOB),
    /// so it is what an <c>ETag</c> is made of.
    /// </summary>
    public string ContentId { get; set; } = default!;
}
