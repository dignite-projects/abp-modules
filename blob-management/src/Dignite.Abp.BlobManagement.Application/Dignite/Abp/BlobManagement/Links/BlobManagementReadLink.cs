using System;
using Volo.Abp.Caching;

namespace Dignite.Abp.BlobManagement.Links;

/// <summary>
/// What a read link carries, signed by <see cref="Tickets.BlobManagementTicketProtector"/>: the tenant and the file.
/// Its expiration is the protector's.
/// </summary>
public class BlobManagementReadLink
{
    /// <summary>The Data Protection purpose of read links.</summary>
    public const string Purpose = "Dignite.Abp.BlobManagement.ReadLink";

    public Guid? TenantId { get; set; }

    public Guid FileId { get; set; }
}

/// <summary>
/// The link one file got for one expiration, kept in the distributed cache until it expires, so the file is handed out
/// at one address per window (<see cref="BlobManagementReadLinkOptions.LinkWindow"/>).
/// </summary>
[CacheName("Dignite.Abp.BlobManagement.ReadLink")]
public class BlobManagementReadLinkCacheItem
{
    public string Link { get; set; } = default!;
}
