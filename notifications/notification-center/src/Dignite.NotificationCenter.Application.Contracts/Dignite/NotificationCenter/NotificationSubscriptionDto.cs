namespace Dignite.NotificationCenter;

public class NotificationSubscriptionDto
{
    public string NotificationName { get; set; } = default!;

    /// <summary>
    /// The definition's group, or <see cref="NotificationCenterConsts.OtherGroupName"/> for a stored subscription whose
    /// definition no longer exists.
    /// </summary>
    public string GroupName { get; set; } = default!;

    /// <summary>Group display name localized for the current reader's culture.</summary>
    public string? GroupDisplayName { get; set; }

    /// <summary>
    /// Stable entity type of an entity-specific subscription, or <see langword="null"/> for a
    /// definition-wide subscription.
    /// </summary>
    public string? EntityTypeName { get; set; }

    /// <summary>
    /// Entity identifier of an entity-specific subscription, or <see langword="null"/> for a
    /// definition-wide subscription.
    /// </summary>
    public string? EntityId { get; set; }

    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    public bool IsSubscribed { get; set; }
}
