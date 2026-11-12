using System;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// Where tickets are redeemed: <c>/api/blob-management/tickets/upload?ticket=</c> (a form <c>POST</c> of the file, field
/// <c>content</c>), <c>/api/blob-management/tickets/download?ticket=</c> (a <c>GET</c>), and for a user's ticket into a user drive
/// <c>/api/blob-management/tickets/upload-file?ticket=</c> (a form <c>POST</c> like the first). The ticket names its tenant itself,
/// so the address carries no <c>__tenant</c>.
/// </summary>
public static class BlobManagementTicketUrl
{
    public const string RoutePrefix = "api/blob-management/tickets";

    public const string UploadPath = "/" + RoutePrefix + "/upload";

    public const string DownloadPath = "/" + RoutePrefix + "/download";

    public const string FileUploadPath = "/" + RoutePrefix + "/upload-file";

    /// <param name="upload">Whether it is an upload ticket (else a download ticket).</param>
    public static string Build(bool upload, string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            throw new ArgumentException("A ticket is required.", nameof(ticket));
        }

        return (upload ? UploadPath : DownloadPath) + "?ticket=" + Uri.EscapeDataString(ticket);
    }

    /// <summary>The address of a user's ticket for adding a file to a user drive.</summary>
    public static string BuildFileUpload(string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            throw new ArgumentException("A ticket is required.", nameof(ticket));
        }

        return FileUploadPath + "?ticket=" + Uri.EscapeDataString(ticket);
    }
}
