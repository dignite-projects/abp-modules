using System;

namespace Dignite.Abp.Notifications;

/// <summary>
/// An in-memory notification being published/distributed (before it becomes per-user store rows), or one read back
/// from the store.
/// </summary>
public class NotificationInfo
{
    public Guid Id { get; set; }

    public string NotificationName { get; set; } = default!;

    /// <summary>
    /// The payload as discriminator-tagged JSON produced by <see cref="INotificationDataSerializer"/>
    /// (e.g. <c>{"type":"Dignite.Message","message":"..."}</c>), or null when the notification carries no data.
    /// </summary>
    /// <remarks>
    /// The payload is serialized once, at the publish boundary, and travels as this string from there on: into the
    /// store, onto every <see cref="NotificationDeliveryRequestedEto"/>, through the distribution background job and
    /// across processes. The process that distributes therefore never needs to know the payload's CLR type; a reader
    /// that wants the typed view calls <see cref="INotificationDataSerializer.Deserialize"/>, which is tolerant.
    /// </remarks>
    public string? DataJson { get; set; }

    public string? EntityTypeName { get; set; }

    public string? EntityId { get; set; }

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public DateTime CreationTime { get; set; }

    /// <summary>
    /// The external channels, when they were already resolved — by a publisher in another process, where the routing
    /// rules of the defining module live. <see langword="null"/> (the default) lets the distributor resolve them with
    /// <see cref="INotificationChannelResolver"/>; an empty array means inbox-only. Not persisted.
    /// </summary>
    public string[]? Channels { get; set; }

    /// <summary>
    /// The authoritative tenant for recipient lookup, eligibility, persistence, and event publication.
    /// <see langword="null"/> explicitly means the host context; distribution never falls back to the caller's
    /// ambient tenant. Direct <see cref="INotificationDistributor"/> callers must populate this for tenant data.
    /// </summary>
    public Guid? TenantId { get; set; }
}
