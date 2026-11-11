using System.Collections.Generic;

namespace Dignite.Drive.Public;

/// <summary>
/// What the caller of <see cref="IDrivePublicAppService.ReadAsync"/> already has and what it wants: the entity tags
/// its client holds, and whether the bytes are wanted at all. The HTTP endpoint fills it from <c>If-None-Match</c> and
/// the request method.
/// </summary>
public class DrivePublicReadRequest
{
    /// <summary>
    /// The entity tags the client already has (<c>If-None-Match</c>), as their opaque values: without the quotes and
    /// without a weak <c>W/</c> prefix, which the comparison ignores.
    /// </summary>
    public List<string> IfNoneMatch { get; set; } = new();

    /// <summary><c>If-None-Match: *</c> - any current file matches.</summary>
    public bool IfNoneMatchAny { get; set; }

    /// <summary>Only the file's information is wanted (a <c>HEAD</c>): nothing is read from storage.</summary>
    public bool HeadersOnly { get; set; }
}
