namespace Dignite.NotificationCenter;

public class NotificationSubscriptionDto
{
    public string NotificationName { get; set; } = default!;

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
