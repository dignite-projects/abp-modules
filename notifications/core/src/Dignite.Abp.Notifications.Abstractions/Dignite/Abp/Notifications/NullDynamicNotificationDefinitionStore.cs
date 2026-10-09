using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications;

/// <summary>
/// No dynamic definitions: a process without <c>Dignite.Abp.Notifications.DefinitionStore</c> knows only its own static
/// ones. Mirrors ABP's <c>NullDynamicFeatureDefinitionStore</c>. Registered with <c>TryRegister</c>, so the store's
/// replacement wins whatever the module order.
/// </summary>
[Dependency(TryRegister = true)]
public class NullDynamicNotificationDefinitionStore : IDynamicNotificationDefinitionStore, ISingletonDependency
{
    private static readonly Task<NotificationDefinition?> CachedNotificationResult =
        Task.FromResult<NotificationDefinition?>(null);

    private static readonly Task<IReadOnlyList<NotificationDefinition>> CachedNotificationsResult =
        Task.FromResult<IReadOnlyList<NotificationDefinition>>(Array.Empty<NotificationDefinition>());

    private static readonly Task<IReadOnlyList<NotificationGroupDefinition>> CachedGroupsResult =
        Task.FromResult<IReadOnlyList<NotificationGroupDefinition>>(Array.Empty<NotificationGroupDefinition>());

    public Task<NotificationDefinition?> GetOrNullAsync(string name)
    {
        return CachedNotificationResult;
    }

    public Task<IReadOnlyList<NotificationDefinition>> GetNotificationsAsync()
    {
        return CachedNotificationsResult;
    }

    public Task<IReadOnlyList<NotificationGroupDefinition>> GetGroupsAsync()
    {
        return CachedGroupsResult;
    }
}
