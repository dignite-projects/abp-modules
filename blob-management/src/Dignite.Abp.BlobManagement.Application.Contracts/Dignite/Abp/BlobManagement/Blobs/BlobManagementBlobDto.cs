namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// What the Blob Management stored for a file: values the application may keep in its own metadata.
/// </summary>
public class BlobManagementBlobDto
{
    /// <summary>The caller's BLOB name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Detected from the stored bytes.</summary>
    public string MimeType { get; set; } = default!;

    /// <summary>The size of the stored bytes, after the Blob Management container's contributors ran.</summary>
    public long Size { get; set; }

    /// <summary>
    /// The SHA-256 (base64) of the uploaded bytes that the caller sent and the module verified; <c>null</c> when the
    /// file was stored without one.
    /// </summary>
    public string? Hash { get; set; }
}
