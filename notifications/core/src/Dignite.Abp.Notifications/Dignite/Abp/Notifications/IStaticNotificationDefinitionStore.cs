using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The definitions this process's <see cref="INotificationDefinitionProvider"/>s register, built once. Mirrors ABP's
/// <c>IStaticFeatureDefinitionStore</c> / <c>IStaticPermissionDefinitionStore</c>: <see cref="INotificationDefinitionManager"/>
/// merges it with <see cref="IDynamicNotificationDefinitionStore"/>, and the definition store saves exactly what it
/// returns — never the definitions other processes saved.
/// </summary>
/// <remarks>
/// Names use ordinal, case-sensitive comparison. Replace the implementation to supply a custom registry; startup
/// validates the registered replacement.
/// </remarks>
public interface IStaticNotificationDefinitionStore
{
    /// <summary>Gets a definition by its ordinal, case-sensitive name, or <see langword="null"/>.</summary>
    Task<NotificationDefinition?> GetOrNullAsync(string name);

    /// <summary>Gets every definition, ordered by group and then registration order.</summary>
    Task<IReadOnlyList<NotificationDefinition>> GetNotificationsAsync();

    /// <summary>Gets every group, in registration order.</summary>
    Task<IReadOnlyList<NotificationGroupDefinition>> GetGroupsAsync();
}
