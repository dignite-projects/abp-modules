namespace Dignite.Drive.Blobs;

/// <summary>
/// What the Drive stored for a file: values the application may keep in its own metadata.
/// </summary>
public class DriveBlobDto
{
    /// <summary>The caller's BLOB name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Detected from the stored bytes.</summary>
    public string MimeType { get; set; } = default!;

    /// <summary>The size of the stored bytes, after the Drive container's contributors ran.</summary>
    public long Size { get; set; }

    /// <summary>SHA-256 of the uploaded bytes, upper-case hex.</summary>
    public string Hash { get; set; } = default!;
}
