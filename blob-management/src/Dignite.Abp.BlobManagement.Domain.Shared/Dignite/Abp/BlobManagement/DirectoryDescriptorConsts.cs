namespace Dignite.Abp.BlobManagement;

public static class DirectoryDescriptorConsts
{
    public static int MaxNameLength { get; set; } = 256;

    /// <summary>
    /// How many levels a directory tree may have: a directory at the top is at depth 1. A constant, not a settable
    /// property like <see cref="MaxNameLength"/>, because <see cref="MaxPathLength"/> - a column's size in a migration -
    /// follows from it.
    /// </summary>
    public const int MaxDepth = 16;

    /// <summary>
    /// The longest <c>DirectoryDescriptor.Path</c>: <see cref="MaxDepth"/> ids of 32 hexadecimal digits
    /// (<c>Guid.ToString("N")</c>) joined by <see cref="PathSeparator"/>.
    /// </summary>
    public const int MaxPathLength = MaxDepth * 32 + (MaxDepth - 1);

    /// <summary>What joins the ids of a <c>DirectoryDescriptor.Path</c>.</summary>
    public const char PathSeparator = '/';

    /// <summary>A <c>DirectoryDescriptor.NameKey</c>: SHA-256 as upper-case hexadecimal.</summary>
    public const int NameKeyLength = 64;
}
