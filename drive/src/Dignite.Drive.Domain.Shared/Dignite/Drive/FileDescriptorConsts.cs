namespace Dignite.Drive;

public static class FileDescriptorConsts
{
    public static int MaxContainerNameLength { get; set; } = 64;

    /// <summary>
    /// The longest <c>Name</c>: in a system container the caller's BLOB name, which may be a path
    /// (<c>2026/11/photo.png</c>), so it is longer than a display name needs.
    /// </summary>
    public static int MaxNameLength { get; set; } = 256;

    /// <summary>
    /// The physical BLOB name the Drive generates: 32 hexadecimal digits and the canonical extension of the
    /// detected type, which is never longer than a few characters.
    /// </summary>
    public static int MaxBlobNameLength { get; set; } = 64;

    public static int MaxMimeTypeLength { get; set; } = 128;

    /// <summary>
    /// SHA-256 as upper-case hex is 64 characters.
    /// </summary>
    public static int MaxHashLength { get; set; } = 64;

    /// <summary>
    /// The longest OAuth client id the Drive records as the writer of a descriptor.
    /// </summary>
    public static int MaxClientIdLength { get; set; } = 128;

    /// <summary>
    /// The dedup key: the tenant (32 hex digits, or <c>host</c>), the container name and the hash, separated by
    /// colons.
    /// </summary>
    public static int MaxDedupKeyLength { get; set; } = 32 + 1 + 64 + 1 + 64;
}
