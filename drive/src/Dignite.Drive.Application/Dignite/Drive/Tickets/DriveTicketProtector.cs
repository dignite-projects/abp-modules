using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Timing;

namespace Dignite.Drive.Tickets;

/// <summary>
/// Signs and reads tickets, and makes an upload ticket good for one use - the mechanism of Site's upload links.
/// <para>
/// A ticket is a <see cref="DriveTicket"/> as JSON, protected by ASP.NET Core Data Protection: tampered with, or signed
/// with another key ring, it does not read. Several Drive instances must share the key ring. An upload's use is recorded
/// in the distributed cache under the ticket's nonce until it would have expired - after the upload succeeded, while a
/// lock named by the nonce is held (<see cref="IAbpDistributedLock"/>): two redemptions at once are not both the first,
/// and an upload that failed (too large, a type the container refuses) can be tried again with the same ticket.
/// </para>
/// </summary>
public class DriveTicketProtector : ITransientDependency
{
    /// <summary>Changes only with the ticket's format: a ticket protected for another purpose does not read here.</summary>
    protected const string Purpose = "Dignite.Drive.Tickets.v1";

    protected const string LockName = "Dignite.Drive.Ticket:";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected IDataProtectionProvider DataProtectionProvider { get; }

    protected IDistributedCache<DriveTicketUse> UseCache { get; }

    protected IAbpDistributedLock DistributedLock { get; }

    protected IClock Clock { get; }

    public DriveTicketProtector(
        IDataProtectionProvider dataProtectionProvider,
        IDistributedCache<DriveTicketUse> useCache,
        IAbpDistributedLock distributedLock,
        IClock clock)
    {
        DataProtectionProvider = dataProtectionProvider;
        UseCache = useCache;
        DistributedLock = distributedLock;
        Clock = clock;
    }

    /// <summary>The protected, URL-safe text of the ticket.</summary>
    public virtual string Protect(DriveTicket ticket)
    {
        return DataProtectionProvider.CreateProtector(Purpose).Protect(JsonSerializer.Serialize(ticket, JsonOptions));
    }

    /// <summary>
    /// The ticket <paramref name="protectedTicket"/> holds, or <c>null</c> when this Drive did not sign it, it is of
    /// another version, or it has expired. Which of those is not told.
    /// </summary>
    public virtual DriveTicket? Read(string? protectedTicket)
    {
        if (string.IsNullOrWhiteSpace(protectedTicket))
        {
            return null;
        }

        try
        {
            var json = DataProtectionProvider.CreateProtector(Purpose).Unprotect(protectedTicket!);
            var ticket = JsonSerializer.Deserialize<DriveTicket>(json, JsonOptions);

            return ticket is { Version: DriveTicket.CurrentVersion } && ticket.ExpiresAt > Clock.Now
                ? ticket
                : null;
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException
                                              or JsonException
                                              or FormatException
                                              or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Runs <paramref name="use"/> once for <paramref name="ticket"/>: <c>(false, default)</c> when the ticket was used
    /// already, or another redemption of it is running - refused rather than waited for. The use is recorded only when
    /// <paramref name="use"/> succeeds.
    /// </summary>
    public virtual async Task<(bool Used, T? Result)> UseOnceAsync<T>(DriveTicket ticket, Func<Task<T>> use)
    {
        await using var handle = await DistributedLock.TryAcquireAsync(LockName + ticket.Nonce);
        if (handle == null || await UseCache.GetAsync(ticket.Nonce) != null)
        {
            return (false, default);
        }

        var result = await use();

        await UseCache.SetAsync(
            ticket.Nonce,
            new DriveTicketUse(),
            new DistributedCacheEntryOptions { AbsoluteExpiration = ticket.ExpiresAt });

        return (true, result);
    }

    /// <summary>When a ticket issued now expires: after <paramref name="lifetimeSeconds"/>, at most the maximum.</summary>
    public virtual DateTime ExpiresAfter(int? lifetimeSeconds)
    {
        var seconds = Math.Min(lifetimeSeconds ?? DriveTicketConsts.DefaultLifetimeSeconds, DriveTicketConsts.MaxLifetimeSeconds);
        return Clock.Now.AddSeconds(seconds);
    }
}
