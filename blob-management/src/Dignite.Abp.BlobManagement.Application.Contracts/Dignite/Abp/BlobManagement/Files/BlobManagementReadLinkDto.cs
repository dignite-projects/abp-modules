using System;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// A read link: the address a browser reads one file at, without signing in, until <see cref="ExpirationTime"/>.
/// </summary>
public class BlobManagementReadLinkDto
{
    public Guid FileId { get; set; }

    /// <summary>
    /// The address, relative to the host that serves Blob Management: <c>/api/blob-management/links/{link}</c>. The
    /// link is a credential for the file - hand it to the user it is for, and do not log it. Asked for again within
    /// the same window it is the same address, so a browser's cache serves it again.
    /// </summary>
    public string Url { get; set; } = default!;

    /// <summary>When the link stops being accepted (UTC), rounded to <c>BlobManagementReadLinkOptions.LinkWindow</c>.</summary>
    public DateTime ExpirationTime { get; set; }
}
