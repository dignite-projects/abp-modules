using System.Collections.Generic;

namespace Dignite.Drive.Public;

/// <summary>
/// Options of the anonymous public read (<see cref="DrivePublicAppService"/>).
/// </summary>
public class DrivePublicReadOptions
{
    /// <summary>
    /// The widths and heights a public read may be resized to. Every size is rendered on the request that asks for
    /// it, and each combination of width and height is a separate cache entry, so an open range would let an
    /// anonymous visitor make the Drive render millions of variants of one picture; a short list bounds that. A host
    /// replaces or extends the list with <c>Configure&lt;DrivePublicReadOptions&gt;</c>; a value above
    /// <see cref="DrivePublicUrl.MaxDimension"/> is never served, whatever the list says.
    /// </summary>
    public List<int> AllowedSizes { get; } = new() { 64, 128, 256, 384, 512, 768, 1024, 1536, 2048, 4096 };
}
