using System;

namespace Dignite.Abp.BlobManagement.Tickets;

public class BlobManagementTicketDto
{
    /// <summary>The signed ticket. Opaque, URL-safe.</summary>
    public string Ticket { get; set; } = default!;

    public BlobManagementTicketKind Kind { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Where the browser redeems it, relative to Blob Management's address: a form <c>POST</c> of the file (field
    /// <c>content</c>) for an upload, a <c>GET</c> for a download (<see cref="BlobManagementTicketUrl"/>).
    /// </summary>
    public string Url { get; set; } = default!;
}
