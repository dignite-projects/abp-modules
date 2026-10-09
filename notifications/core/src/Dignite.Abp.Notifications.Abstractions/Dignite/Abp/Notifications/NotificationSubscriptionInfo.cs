using System;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Describes one subscription identity. <see cref="EntityTypeName"/> and <see cref="EntityId"/> must either both
/// be null (all instances of the notification definition) or both be present (one concrete entity).
/// </summary>
public class NotificationSubscriptionInfo
{
    public Guid UserId { get; set; }

    public string NotificationName { get; set; } = default!;

    public string? EntityTypeName { get; set; }

    public string? EntityId { get; set; }

    public DateTime CreationTime { get; set; }

    public Guid? TenantId { get; set; }
}
