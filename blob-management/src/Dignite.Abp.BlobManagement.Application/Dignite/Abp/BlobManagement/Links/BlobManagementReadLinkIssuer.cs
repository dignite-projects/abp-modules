using System;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Tickets;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobManagement.Links;

/// <summary>
/// Issues read links for the file services, which authorize the caller first. A link names the tenant and the file and
/// expires at its lifetime rounded up to <see cref="BlobManagementReadLinkOptions.LinkWindow"/>; the link issued for a
/// file and an expiration is kept in the distributed cache until then, so the same file asked for within one window
/// gets the same address - and a browser's cache, keyed by the address, serves it again. A cache that misses or fails
/// costs only a new link for the same file.
/// </summary>
public class BlobManagementReadLinkIssuer : ITransientDependency
{
    protected BlobManagementTicketProtector TicketProtector { get; }

    protected IDistributedCache<BlobManagementReadLinkCacheItem> Cache { get; }

    protected BlobManagementReadLinkOptions Options { get; }

    public BlobManagementReadLinkIssuer(
        BlobManagementTicketProtector ticketProtector,
        IDistributedCache<BlobManagementReadLinkCacheItem> cache,
        IOptions<BlobManagementReadLinkOptions> options)
    {
        TicketProtector = ticketProtector;
        Cache = cache;
        Options = options.Value;
    }

    /// <summary>
    /// The link to <paramref name="file"/>, valid for <paramref name="lifetimeInSeconds"/> (the option's default, at
    /// most its maximum). A pending file gets none (<see cref="BlobManagementErrorCodes.FilePending"/>): its business
    /// system has not confirmed it.
    /// </summary>
    public virtual async Task<BlobManagementReadLinkDto> IssueAsync(FileDescriptor file, int? lifetimeInSeconds)
    {
        if (file.IsPending)
        {
            throw new BusinessException(BlobManagementErrorCodes.FilePending);
        }

        var expiration = GetExpiration(DateTimeOffset.UtcNow, lifetimeInSeconds);
        var key = (file.TenantId?.ToString("N") ?? "host") + ":" + file.Id.ToString("N") + ":" +
                  expiration.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var cached = await Cache.GetAsync(key);
        var link = cached?.Link;
        if (link == null)
        {
            link = TicketProtector.Protect(
                BlobManagementReadLink.Purpose,
                new BlobManagementReadLink { TenantId = file.TenantId, FileId = file.Id },
                expiration);
            await Cache.SetAsync(key, new BlobManagementReadLinkCacheItem { Link = link },
                new DistributedCacheEntryOptions { AbsoluteExpiration = expiration });
        }

        return new BlobManagementReadLinkDto
        {
            FileId = file.Id,
            Url = BlobManagementReadLinkUrl.Build(link),
            ExpirationTime = expiration.UtcDateTime
        };
    }

    /// <summary>
    /// <paramref name="now"/> plus the lifetime, rounded up to the window - or down, when rounding up would take the
    /// link past <see cref="BlobManagementReadLinkOptions.MaxLinkLifetime"/>.
    /// </summary>
    protected virtual DateTimeOffset GetExpiration(DateTimeOffset now, int? lifetimeInSeconds)
    {
        var lifetime = lifetimeInSeconds is > 0 ? TimeSpan.FromSeconds(lifetimeInSeconds.Value) : Options.LinkLifetime;
        if (lifetime > Options.MaxLinkLifetime)
        {
            lifetime = Options.MaxLinkLifetime;
        }

        var target = now + lifetime;
        var window = Options.LinkWindow.Ticks;
        if (window <= 0)
        {
            return target;
        }

        var up = new DateTimeOffset((target.UtcTicks + window - 1) / window * window, TimeSpan.Zero);
        if (up - now <= Options.MaxLinkLifetime)
        {
            return up;
        }

        var down = up.AddTicks(-window);
        return down > now ? down : target;
    }
}
