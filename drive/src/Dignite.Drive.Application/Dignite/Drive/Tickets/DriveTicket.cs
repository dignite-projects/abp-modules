using System;

namespace Dignite.Drive.Tickets;

/// <summary>
/// What a ticket says, signed as a whole by <see cref="DriveTicketProtector"/>: nothing in it can be changed, and the
/// expiry is part of what is signed.
/// </summary>
public class DriveTicket
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public DriveTicketKind Kind { get; set; }

    /// <summary>The tenant the file belongs to; the redemption runs in it, whatever the request's tenant.</summary>
    public Guid? TenantId { get; set; }

    public string ContainerName { get; set; } = default!;

    public string BlobName { get; set; } = default!;

    public long? MaxSizeInBytes { get; set; }

    public bool OverrideExisting { get; set; }

    /// <summary>The client that issued it, checked again at redemption; <c>null</c> for a trusted in-process issuer.</summary>
    public string? ClientId { get; set; }

    /// <summary>Random; an upload ticket's use is recorded under it.</summary>
    public string Nonce { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }
}

/// <summary>The cache entry of an upload ticket that has been used; its key, the nonce, is all that matters.</summary>
public class DriveTicketUse
{
}
