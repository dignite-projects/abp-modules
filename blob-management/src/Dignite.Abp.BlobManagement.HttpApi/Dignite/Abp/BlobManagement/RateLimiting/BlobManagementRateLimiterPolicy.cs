using System;
using System.Globalization;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Dignite.Abp.BlobManagement.RateLimiting;

/// <summary>
/// One of <see cref="BlobManagementRateLimiting"/>'s policies: a fixed window of a minute per client address, with the
/// limit <see cref="BlobManagementRateLimitingOptions"/> gives the kind of request. A request past it is answered
/// <c>429</c> with <c>Retry-After</c>, whatever the host's own rejection status is.
/// </summary>
public class BlobManagementRateLimiterPolicy : IRateLimiterPolicy<string>
{
    protected static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>The policy's name, one of <see cref="BlobManagementRateLimiting"/>'s.</summary>
    public string PolicyName { get; }

    public BlobManagementRateLimiterPolicy(string policyName)
    {
        PolicyName = policyName;
    }

    public virtual Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => (context, _) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers[HeaderNames.RetryAfter] =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };

    public virtual RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var options = httpContext.RequestServices.GetRequiredService<IOptions<BlobManagementRateLimitingOptions>>().Value;
        var (kind, permitLimit) = GetKindAndLimit(httpContext, options);
        if (permitLimit <= 0)
        {
            return RateLimitPartition.GetNoLimiter(kind);
        }

        var client = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(kind + ":" + client, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = Window,
            QueueLimit = 0
        });
    }

    /// <summary>
    /// The kind of request - a partition of its own - and its limit per minute. A public read that asks for a size is a
    /// rendition, limited apart from reads as stored.
    /// </summary>
    protected virtual (string Kind, int PermitLimit) GetKindAndLimit(HttpContext httpContext, BlobManagementRateLimitingOptions options)
    {
        switch (PolicyName)
        {
            case BlobManagementRateLimiting.PublicReads:
                return AsksForSize(httpContext.Request)
                    ? ("renditions", options.RenditionsPerMinute)
                    : ("reads", options.PublicReadsPerMinute);
            case BlobManagementRateLimiting.Uploads:
                return ("uploads", options.UploadsPerMinute);
            case BlobManagementRateLimiting.ReadLinks:
                return ("links", options.ReadLinksPerMinute);
            default:
                return (PolicyName, 0);
        }
    }

    /// <summary>Whether a public read asks for a width or a height, the query its address takes.</summary>
    protected virtual bool AsksForSize(HttpRequest request)
    {
        return IsPositive(request.Query["Width"]) || IsPositive(request.Query["Height"]);
    }

    private static bool IsPositive(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > 0;
    }
}
