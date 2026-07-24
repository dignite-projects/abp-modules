namespace Dignite.Abp.Notifications;

public interface INotificationDefinitionContext
{
    /// <summary>
    /// Adds a definition. Definition names use ordinal, case-sensitive comparison, and every repeated name is
    /// rejected even when the definitions appear equivalent; mutable definitions have no safe idempotent equality.
    /// </summary>
    NotificationDefinition Add(NotificationDefinition definition);

    /// <summary>
    /// Gets a definition by its ordinal, case-sensitive name.
    /// </summary>
    NotificationDefinition? GetOrNull(string name);
}
