using System;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The address a browser uploads to with a ticket: <c>/api/blob-management/uploads/{ticket}</c>. One definition for the
/// endpoint and for the services that hand tickets out.
/// </summary>
public static class BlobManagementUploadUrl
{
    public const string RoutePrefix = "api/blob-management/uploads";

    /// <summary>
    /// The path of the upload with <paramref name="ticket"/>, relative to Blob Management's address. A ticket is
    /// base64url and needs no escaping; it is escaped anyway.
    /// </summary>
    public static string Build(string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            throw new ArgumentException("A ticket is required.", nameof(ticket));
        }

        return "/" + RoutePrefix + "/" + Uri.EscapeDataString(ticket);
    }
}
