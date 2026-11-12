using System;
using Volo.Abp.ObjectExtending;

namespace Dignite.Abp.BlobManagement.Containers;

/// <summary>
/// A user drive: its configuration as a client needs it to offer an upload. Extensible: what a module building on the
/// Blob Management adds to a container travels in <c>ExtraProperties</c>.
/// </summary>
public class BlobManagementContainerDto : ExtensibleObject
{
    public string Name { get; set; } = default!;

    /// <summary>Whether its files are read publicly; their <c>Url</c> is then set.</summary>
    public bool PublicRead { get; set; }

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
}
