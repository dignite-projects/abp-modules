using Volo.Abp.Localization;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Passed to <see cref="INotificationDefinitionProvider.Define"/>. Mirrors ABP's permission definition context:
/// every definition is added through a group (<see cref="AddGroup"/> → <see cref="NotificationGroupDefinition.AddNotification"/>).
/// </summary>
public interface INotificationDefinitionContext
{
    /// <summary>
    /// Adds a group. Group names use ordinal, case-sensitive comparison, and a repeated name is rejected; to add
    /// definitions to a group another provider created, use <see cref="GetGroupOrNull"/>.
    /// </summary>
    NotificationGroupDefinition AddGroup(string name, ILocalizableString? displayName = null);

    /// <summary>Gets a group by its ordinal, case-sensitive name, or <see langword="null"/>.</summary>
    NotificationGroupDefinition? GetGroupOrNull(string name);

    /// <summary>
    /// Gets a definition from any group by its ordinal, case-sensitive name. Definition names are unique across all
    /// groups, and every repeated name is rejected even when the definitions appear equivalent.
    /// </summary>
    NotificationDefinition? GetOrNull(string name);
}
