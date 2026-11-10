using System;

namespace Dignite.Drive;

/// <summary>
/// Where tickets are redeemed: <c>/api/drive/tickets/upload?ticket=</c> (a form <c>POST</c> of the file, field
/// <c>content</c>) and <c>/api/drive/tickets/download?ticket=</c> (a <c>GET</c>). The ticket names its tenant itself, so
/// the address carries no <c>__tenant</c>.
/// </summary>
public static class DriveTicketUrl
{
    public const string RoutePrefix = "api/drive/tickets";

    public const string UploadPath = "/" + RoutePrefix + "/upload";

    public const string DownloadPath = "/" + RoutePrefix + "/download";

    /// <param name="upload">Whether it is an upload ticket (else a download ticket).</param>
    public static string Build(bool upload, string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            throw new ArgumentException("A ticket is required.", nameof(ticket));
        }

        return (upload ? UploadPath : DownloadPath) + "?ticket=" + Uri.EscapeDataString(ticket);
    }
}
