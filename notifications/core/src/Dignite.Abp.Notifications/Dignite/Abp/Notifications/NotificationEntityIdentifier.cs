using Volo.Abp;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Identifies the entity a notification relates to (e.g. a specific order or post).
/// </summary>
/// <remarks>
/// <see cref="EntityTypeName"/> is a stable, caller-chosen string — never a CLR type name. It is persisted on
/// <c>Notification</c> and <c>NotificationSubscription</c>, matched by string equality when resolving subscribers,
/// returned over REST, and used as the key of the UI's entity-link resolvers. A CLR name would silently orphan
/// every stored subscription the day a namespace is renamed. Same rule <c>[NotificationDataType]</c> enforces for
/// payloads — see <c>notifications-invariants.md</c> §1.
/// </remarks>
public class NotificationEntityIdentifier
{
    /// <summary>A short, stable name for the entity type, e.g. <c>"Demo.Order"</c>.</summary>
    public string EntityTypeName { get; }

    /// <summary>The entity's identifier, rendered as a string.</summary>
    public string EntityId { get; }

    public NotificationEntityIdentifier(string entityTypeName, string entityId)
    {
        EntityTypeName = Check.NotNullOrWhiteSpace(entityTypeName, nameof(entityTypeName));
        EntityId = Check.NotNullOrWhiteSpace(entityId, nameof(entityId));
    }
}
