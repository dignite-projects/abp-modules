using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The notification definitions this process can see — its own (<see cref="IStaticNotificationDefinitionStore"/>) and,
/// with a definition store installed, those other processes saved (<see cref="IDynamicNotificationDefinitionStore"/>) —
/// and the per-user availability check. A static definition wins over a dynamic one of the same name, as in ABP's
/// <c>FeatureDefinitionManager</c>. Name registration and lookup use ordinal, case-sensitive comparison.
/// </summary>
/// <remarks>
/// Asynchronous because the dynamic definitions are read from a store, as ABP made <c>IPermissionDefinitionManager</c>
/// asynchronous in 6.0. The interface stays replaceable; to supply a custom definition registry, replace
/// <see cref="IStaticNotificationDefinitionStore"/> instead, which startup validates.
/// </remarks>
public interface INotificationDefinitionManager
{
    /// <summary>Gets a definition by its ordinal, case-sensitive name; throws when no definition has it.</summary>
    Task<NotificationDefinition> GetAsync(string name);

    /// <summary>Gets a definition by its ordinal, case-sensitive name, or <see langword="null"/>.</summary>
    Task<NotificationDefinition?> GetOrNullAsync(string name);

    /// <summary>
    /// Gets every definition: the static ones ordered by group and then registration order, followed by the dynamic
    /// ones whose names no static definition has.
    /// </summary>
    Task<IReadOnlyList<NotificationDefinition>> GetAllAsync();

    /// <summary>
    /// Gets every group: the static ones in registration order, followed by the dynamic ones whose names no static group
    /// has. Use <see cref="NotificationDefinition.GroupName"/> on <see cref="GetAllAsync"/> to list a group's
    /// definitions across both sources.
    /// </summary>
    Task<IReadOnlyList<NotificationGroupDefinition>> GetGroupsAsync();

    /// <summary>Gets a group by its ordinal, case-sensitive name (static first), or <see langword="null"/>.</summary>
    Task<NotificationGroupDefinition?> GetGroupOrNullAsync(string name);

    /// <summary>
    /// Evaluates whether the user may subscribe to and receive the notification in the ambient tenant/host context.
    /// An unknown name is never available.
    /// </summary>
    Task<bool> IsAvailableAsync(string name, Guid userId);

    Task<IReadOnlyList<NotificationDefinition>> GetAllAvailableAsync(Guid userId);
}
