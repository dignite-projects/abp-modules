using System;
using Dignite.Abp.BlobManagement.Public;

namespace Dignite.Abp.BlobManagement.Links;

/// <summary>One read with a link, answered from one lookup of the file, as a public read is.</summary>
public class BlobManagementReadLinkResult : BlobManagementPublicReadResult
{
    /// <summary>When the link stops being accepted (UTC): a cache may keep the response no longer.</summary>
    public DateTime ExpirationTime { get; set; }
}
