namespace Dignite.Abp.BlobManagement.RateLimiting;

/// <summary>
/// How many requests one client address may make to each of Blob Management's anonymous addresses in a minute; a
/// request past the limit is answered <c>429</c> with <c>Retry-After</c>. <c>0</c> sets no limit. Each limit is the
/// address's own: a client that reaches one is still served by the others.
/// <para>
/// The client address is the connection's (<c>HttpContext.Connection.RemoteIpAddress</c>). Behind a reverse proxy or a
/// CDN, the host configures ASP.NET Core's forwarded headers so that it is the client's; otherwise it is the proxy's,
/// and one limit is shared by everyone behind it.
/// </para>
/// </summary>
public class BlobManagementRateLimitingOptions
{
    /// <summary>
    /// Public reads that ask for a size (<c>?Width=</c>, <c>?Height=</c>): each is rendered when it is asked for, so this
    /// is the limit that protects the server's processor. A CDN in front of <c>/blobs</c> serves repeated reads without
    /// asking.
    /// </summary>
    public int RenditionsPerMinute { get; set; } = 120;

    /// <summary>Public reads of a file as stored. No limit by default: the bytes are streamed, not processed.</summary>
    public int PublicReadsPerMinute { get; set; }

    /// <summary>Uploads with a ticket.</summary>
    public int UploadsPerMinute { get; set; } = 60;

    /// <summary>Reads with a link. No limit by default: a page of a business system may show many files at once.</summary>
    public int ReadLinksPerMinute { get; set; }
}
