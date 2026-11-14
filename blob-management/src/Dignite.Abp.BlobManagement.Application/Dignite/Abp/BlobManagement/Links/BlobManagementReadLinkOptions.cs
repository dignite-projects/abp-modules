using System;

namespace Dignite.Abp.BlobManagement.Links;

/// <summary>
/// Read links: how long they are valid, and the window their expiration is rounded to. A host changes them with
/// <c>Configure&lt;BlobManagementReadLinkOptions&gt;</c>.
/// </summary>
public class BlobManagementReadLinkOptions
{
    /// <summary>How long a link is valid when its issuer names no lifetime. Default: 10 minutes.</summary>
    public TimeSpan LinkLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The longest lifetime an issuer may ask for; a longer one is cut to it, and rounding never takes a link past it.
    /// A link is a credential in a URL, so it is kept short. Default: 1 hour.
    /// </summary>
    public TimeSpan MaxLinkLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// The window a link's expiration is rounded up to: the links one file gets within a window are one link, so a
    /// browser's cache serves the file again rather than read it anew from a new address. A link lives at least its
    /// lifetime and less than its lifetime plus a window (never past <see cref="MaxLinkLifetime"/>). Default: 5 minutes.
    /// </summary>
    public TimeSpan LinkWindow { get; set; } = TimeSpan.FromMinutes(5);
}
