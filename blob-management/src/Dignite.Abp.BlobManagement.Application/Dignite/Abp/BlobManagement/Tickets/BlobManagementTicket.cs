using System;
using System.Collections.Generic;
using System.Security.Claims;
using Volo.Abp.Security.Claims;

namespace Dignite.Abp.BlobManagement.Tickets;

/// <summary>
/// What a ticket says, signed as a whole by <see cref="BlobManagementTicketProtector"/>: nothing in it can be changed, and the
/// expiry is part of what is signed.
/// </summary>
public class BlobManagementTicket
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public BlobManagementTicketKind Kind { get; set; }

    /// <summary>The tenant the file belongs to; the redemption runs in it, whatever the request's tenant.</summary>
    public Guid? TenantId { get; set; }

    public string ContainerName { get; set; } = default!;

    public string BlobName { get; set; } = default!;

    public long? MaxSizeInBytes { get; set; }

    public bool OverrideExisting { get; set; }

    /// <summary>The client that issued it, checked again at redemption; <c>null</c> for a trusted in-process issuer.</summary>
    public string? ClientId { get; set; }

    /// <summary>A <see cref="BlobManagementTicketKind.FileUpload"/> ticket: the name the file gets.</summary>
    public string? FileName { get; set; }

    /// <summary>
    /// A <see cref="BlobManagementTicketKind.FileUpload"/> ticket: the user who issued it, as whom it is redeemed - their
    /// permissions are evaluated again then, with the roles they had when they issued it.
    /// </summary>
    public Guid? UserId { get; set; }

    public string? UserName { get; set; }

    public string[]? Roles { get; set; }

    /// <summary>Random; an upload ticket's use is recorded under it.</summary>
    public string Nonce { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// The identity a <see cref="BlobManagementTicketKind.FileUpload"/> ticket is redeemed as.
/// </summary>
public static class BlobManagementTicketPrincipal
{
    public const string AuthenticationType = "BlobManagementTicket";

    public static ClaimsPrincipal Create(BlobManagementTicket ticket)
    {
        var claims = new List<Claim> { new(AbpClaimTypes.UserId, ticket.UserId!.Value.ToString()) };
        if (!string.IsNullOrEmpty(ticket.UserName))
        {
            claims.Add(new Claim(AbpClaimTypes.UserName, ticket.UserName!));
        }

        if (ticket.TenantId.HasValue)
        {
            claims.Add(new Claim(AbpClaimTypes.TenantId, ticket.TenantId.Value.ToString()));
        }

        foreach (var role in ticket.Roles ?? Array.Empty<string>())
        {
            claims.Add(new Claim(AbpClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType, AbpClaimTypes.UserName, AbpClaimTypes.Role));
    }
}

/// <summary>The cache entry of an upload ticket that has been used; its key, the nonce, is all that matters.</summary>
public class BlobManagementTicketUse
{
}
