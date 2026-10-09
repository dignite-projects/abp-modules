using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Features;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Merges the static and dynamic definitions — static first, a dynamic definition or group only when no static one has
/// its name, exactly as ABP's <c>FeatureDefinitionManager</c> does — and evaluates per-user availability.
/// </summary>
/// <remarks>
/// Transient, like ABP's <c>PermissionDefinitionManager</c>: the dynamic store reads repositories, so the manager must
/// not outlive a request. The definitions themselves are cached by the singleton stores. Feature and permission checks
/// are still resolved from a fresh service scope per call, so a consumer's singleton that holds this manager never
/// captures request-scoped authorization services (the reference implementation's lifetime bug).
/// </remarks>
public class NotificationDefinitionManager : INotificationDefinitionManager, ITransientDependency
{
    protected IStaticNotificationDefinitionStore StaticStore { get; }

    protected IDynamicNotificationDefinitionStore DynamicStore { get; }

    protected IServiceScopeFactory ServiceScopeFactory { get; }

    public NotificationDefinitionManager(
        IStaticNotificationDefinitionStore staticStore,
        IDynamicNotificationDefinitionStore dynamicStore,
        IServiceScopeFactory serviceScopeFactory)
    {
        StaticStore = staticStore;
        DynamicStore = dynamicStore;
        ServiceScopeFactory = serviceScopeFactory;
    }

    public virtual async Task<NotificationDefinition> GetAsync(string name)
    {
        return await GetOrNullAsync(name) ?? throw new AbpException($"Undefined notification: {name}");
    }

    public virtual async Task<NotificationDefinition?> GetOrNullAsync(string name)
    {
        Check.NotNull(name, nameof(name));

        return await StaticStore.GetOrNullAsync(name) ?? await DynamicStore.GetOrNullAsync(name);
    }

    public virtual async Task<IReadOnlyList<NotificationDefinition>> GetAllAsync()
    {
        var staticDefinitions = await StaticStore.GetNotificationsAsync();
        var staticNames = staticDefinitions.Select(definition => definition.Name).ToHashSet(StringComparer.Ordinal);

        return staticDefinitions
            .Concat((await DynamicStore.GetNotificationsAsync()).Where(definition => !staticNames.Contains(definition.Name)))
            .ToList();
    }

    public virtual async Task<IReadOnlyList<NotificationGroupDefinition>> GetGroupsAsync()
    {
        var staticGroups = await StaticStore.GetGroupsAsync();
        var staticNames = staticGroups.Select(group => group.Name).ToHashSet(StringComparer.Ordinal);

        return staticGroups
            .Concat((await DynamicStore.GetGroupsAsync()).Where(group => !staticNames.Contains(group.Name)))
            .ToList();
    }

    public virtual async Task<NotificationGroupDefinition?> GetGroupOrNullAsync(string name)
    {
        Check.NotNull(name, nameof(name));

        return (await StaticStore.GetGroupsAsync()).FirstOrDefault(group => group.Name == name)
               ?? (await DynamicStore.GetGroupsAsync()).FirstOrDefault(group => group.Name == name);
    }

    public virtual async Task<bool> IsAvailableAsync(string name, Guid userId)
    {
        var definition = await GetOrNullAsync(name);
        if (definition == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(definition.FeatureName))
        {
            using var scope = ServiceScopeFactory.CreateScope();
            var featureChecker = scope.ServiceProvider.GetRequiredService<IFeatureChecker>();
            if (!await featureChecker.IsEnabledAsync(definition.FeatureName!))
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(definition.PermissionName))
        {
            if (!await CheckPermissionAsync(definition.PermissionName!, userId))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Delegates to <see cref="INotificationPermissionChecker"/>, resolved from a fresh scope per call (see the class
    /// remarks). The default checker grants everything; the optional Identity integration replaces it with a real check.
    /// </summary>
    protected virtual async Task<bool> CheckPermissionAsync(string permissionName, Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var permissionChecker = scope.ServiceProvider.GetRequiredService<INotificationPermissionChecker>();
        return await permissionChecker.IsGrantedAsync(userId, permissionName);
    }

    public virtual async Task<IReadOnlyList<NotificationDefinition>> GetAllAvailableAsync(Guid userId)
    {
        var result = new List<NotificationDefinition>();
        foreach (var definition in await GetAllAsync())
        {
            if (await IsAvailableAsync(definition.Name, userId))
            {
                result.Add(definition);
            }
        }

        return result;
    }
}
