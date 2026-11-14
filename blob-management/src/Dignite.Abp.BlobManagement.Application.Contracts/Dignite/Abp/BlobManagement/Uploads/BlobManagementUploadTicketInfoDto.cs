using System;

namespace Dignite.Abp.BlobManagement.Uploads;

/// <summary>What an upload ticket allows, as the upload endpoint needs it before it reads the body.</summary>
public class BlobManagementUploadTicketInfoDto
{
    public Guid FileId { get; set; }

    /// <summary>The largest file the ticket takes, in bytes.</summary>
    public long MaxSize { get; set; }

    /// <summary>When the ticket stops being accepted (UTC).</summary>
    public DateTime ExpirationTime { get; set; }
}
