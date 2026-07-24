using System;

namespace Dignite.Abp.Notifications;

/// <summary>
/// An in-memory notification being published/distributed (before it becomes per-user store rows).
/// </summary>
public class NotificationInfo
{
    public Guid Id { get; set; }

    public string NotificationName { get; set; } = default!;

    public NotificationData? Data { get; set; }

    public string? EntityTypeName { get; set; }

    public string? EntityId { get; set; }

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public DateTime CreationTime { get; set; }

    /// <summary>
    /// The authoritative tenant for recipient lookup, eligibility, persistence, and event publication.
    /// <see langword="null"/> explicitly means the host context; distribution never falls back to the caller's
    /// ambient tenant. Direct <see cref="INotificationDistributor"/> callers must populate this for tenant data.
    /// </summary>
    public Guid? TenantId { get; set; }
}
