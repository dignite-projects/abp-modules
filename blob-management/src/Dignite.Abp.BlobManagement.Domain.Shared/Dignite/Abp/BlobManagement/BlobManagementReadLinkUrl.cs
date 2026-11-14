using System;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The address a browser reads a file at with a read link: <c>/api/blob-management/links/{link}</c>. One definition for
/// the endpoint and for the services that hand links out.
/// </summary>
public static class BlobManagementReadLinkUrl
{
    public const string RoutePrefix = "api/blob-management/links";

    /// <summary>The path of the read with <paramref name="link"/>, relative to Blob Management's address.</summary>
    public static string Build(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            throw new ArgumentException("A link is required.", nameof(link));
        }

        return "/" + RoutePrefix + "/" + Uri.EscapeDataString(link);
    }
}
