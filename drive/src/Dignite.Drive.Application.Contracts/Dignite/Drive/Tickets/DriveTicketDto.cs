using System;

namespace Dignite.Drive.Tickets;

public class DriveTicketDto
{
    /// <summary>The signed ticket. Opaque, URL-safe.</summary>
    public string Ticket { get; set; } = default!;

    public DriveTicketKind Kind { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Where the browser redeems it, relative to the Drive's address: a form <c>POST</c> of the file (field
    /// <c>content</c>) for an upload, a <c>GET</c> for a download (<see cref="DriveTicketUrl"/>).
    /// </summary>
    public string Url { get; set; } = default!;
}
