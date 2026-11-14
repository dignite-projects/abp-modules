using System;

namespace Dignite.Abp.BlobManagement.Uploads;

/// <summary>
/// Uploads through a ticket: how long a ticket may be used, and how long a file it brought in may stay pending before
/// it is deleted as abandoned. A host changes them with <c>Configure&lt;BlobManagementUploadOptions&gt;</c>.
/// </summary>
public class BlobManagementUploadOptions
{
    /// <summary>How long a ticket is valid when its issuer names no lifetime. Default: 5 minutes.</summary>
    public TimeSpan TicketLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The longest lifetime an issuer may ask for; a longer one is cut to it. A ticket is a credential in a URL, so it
    /// is kept short. Default: 1 hour.
    /// </summary>
    public TimeSpan MaxTicketLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// How long a file uploaded through a ticket may stay pending - not confirmed by its business system - before the
    /// cleanup deletes it. Default: 24 hours.
    /// </summary>
    public TimeSpan PendingLifetime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>How often the cleanup looks for abandoned pending files. Default: 1 hour.</summary>
    public TimeSpan CleanupPeriod { get; set; } = TimeSpan.FromHours(1);
}
