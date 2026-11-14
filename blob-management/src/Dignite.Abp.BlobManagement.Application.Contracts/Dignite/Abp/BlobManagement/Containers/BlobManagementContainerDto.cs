using System;
using Volo.Abp.ObjectExtending;

namespace Dignite.Abp.BlobManagement.Containers;

/// <summary>
/// A managed container: its configuration as a file input needs it to offer an upload, and what the caller may do
/// with its directories.
/// </summary>
public class BlobManagementContainerDto : ExtensibleObject
{
    public string Name { get; set; } = default!;

    /// <summary>Whether the container has a directory tree. Without one its files are one flat list.</summary>
    public bool AllowDirectories { get; set; }

    /// <summary>
    /// The largest upload the container accepts, in bytes: its <c>MaxSize</c> contributor, or the pipeline's buffering
    /// cap without one.
    /// </summary>
    public long MaxFileSize { get; set; }

    /// <summary>
    /// The extensions of the content types the container allows (checked against the detected content); empty when it
    /// restricts no type.
    /// </summary>
    public string[] AllowedFileExtensions { get; set; } = Array.Empty<string>();

    /// <summary>The width a picture is scaled down to, when the container resizes images.</summary>
    public int? ImageMaxWidth { get; set; }

    /// <summary>The height a picture is scaled down to, when the container resizes images.</summary>
    public int? ImageMaxHeight { get; set; }

    /// <summary>What the caller may do with the container's directories.</summary>
    public BlobManagementAccessDto Access { get; set; } = new();
}
