namespace Dignite.Abp.BlobManagement.RateLimiting;

/// <summary>
/// The ASP.NET Core rate limiting policies of Blob Management's anonymous addresses, one per address, each registered
/// by <see cref="BlobManagementHttpApiModule"/> and named on its controller with <c>[EnableRateLimiting]</c>. They take
/// effect where the host runs ASP.NET Core's rate limiter after routing (<c>app.UseRateLimiter()</c> after
/// <c>app.UseRouting()</c>); the limits are <see cref="BlobManagementRateLimitingOptions"/>.
/// </summary>
public static class BlobManagementRateLimiting
{
    /// <summary>Public reads at <c>/blobs</c>: as stored, and renditions of a size.</summary>
    public const string PublicReads = "Dignite.Abp.BlobManagement.PublicReads";

    /// <summary>Uploads with a ticket.</summary>
    public const string Uploads = "Dignite.Abp.BlobManagement.Uploads";

    /// <summary>Reads with a link.</summary>
    public const string ReadLinks = "Dignite.Abp.BlobManagement.ReadLinks";
}
