using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Definitions this process does not define itself — the ones other processes saved to a shared definition store.
/// Mirrors ABP's <c>IDynamicFeatureDefinitionStore</c> / <c>IDynamicPermissionDefinitionStore</c>. The contract lives in
/// Abstractions so that <see cref="INotificationDefinitionManager"/> can merge it without depending on a store; the default
/// (<see cref="NullDynamicNotificationDefinitionStore"/>) has nothing, and <c>Dignite.Abp.Notifications.Domain</c>
/// replaces it.
/// </summary>
public interface IDynamicNotificationDefinitionStore
{
    /// <summary>Gets a definition by its ordinal, case-sensitive name, or <see langword="null"/>.</summary>
    Task<NotificationDefinition?> GetOrNullAsync(string name);

    /// <summary>Gets every definition.</summary>
    Task<IReadOnlyList<NotificationDefinition>> GetNotificationsAsync();

    /// <summary>Gets every group, with its definitions.</summary>
    Task<IReadOnlyList<NotificationGroupDefinition>> GetGroupsAsync();
}
