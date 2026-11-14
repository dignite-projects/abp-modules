using System;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// An upload ticket, for the business system to hand to its browser: the browser sends one file to
/// <see cref="UploadUrl"/>, without signing in, before <see cref="ExpirationTime"/>.
/// </summary>
public class BlobManagementUploadTicketDto
{
    /// <summary>The ticket itself: signed by Blob Management, opaque to everyone else. A credential - do not log it.</summary>
    public string Ticket { get; set; } = default!;

    /// <summary>
    /// Where the browser sends the file (<c>POST</c>, the file in the form field <c>file</c>), relative to the host that
    /// serves Blob Management: <c>/api/blob-management/uploads/{ticket}</c>.
    /// </summary>
    public string UploadUrl { get; set; } = default!;

    /// <summary>
    /// The id the file will have, given in advance: the business system keeps it with its record, and confirms the
    /// file by it once the browser reports the upload done.
    /// </summary>
    public Guid FileId { get; set; }

    /// <summary>When the ticket stops being accepted (UTC).</summary>
    public DateTime ExpirationTime { get; set; }
}
