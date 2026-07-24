using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Features;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Holds the notification definition registry (built once from providers) and evaluates per-user availability.
/// The registry is a singleton, but feature/permission checks are request-scoped: they are resolved from a fresh
/// service scope per call, so this singleton never captures scoped services (fixes the reference's lifetime bug).
/// </summary>
public class NotificationDefinitionManager : INotificationDefinitionManager, ISingletonDependency
{
    protected NotificationDefinitionRegistration Registration { get; }

    protected IServiceScopeFactory ServiceScopeFactory { get; }

    private readonly Lazy<IDictionary<string, NotificationDefinition>> _definitions;

    public NotificationDefinitionManager(
        IOptions<NotificationDefinitionRegistration> registration,
        IServiceScopeFactory serviceScopeFactory)
    {
        Registration = registration.Value;
        ServiceScopeFactory = serviceScopeFactory;
        _definitions = new Lazy<IDictionary<string, NotificationDefinition>>(CreateDefinitions, isThreadSafe: true);
    }

    public NotificationDefinition Get(string name)
    {
        return GetOrNull(name) ?? throw new AbpException($"Undefined notification: {name}");
    }

    public NotificationDefinition? GetOrNull(string name)
    {
        return _definitions.Value.TryGetValue(name, out var definition) ? definition : null;
    }

    public IReadOnlyList<NotificationDefinition> GetAll()
    {
        return _definitions.Value.Values.ToList();
    }

    public virtual async Task<bool> IsAvailableAsync(string name, Guid userId)
    {
        var definition = GetOrNull(name);
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
    /// Delegates to <see cref="INotificationPermissionChecker"/>, resolved from a fresh scope per call so this
    /// singleton never captures request-scoped authorization services (fixes the reference's lifetime bug). The
    /// default checker grants everything; the optional Identity integration replaces it with a real check.
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
        foreach (var definition in GetAll())
        {
            if (await IsAvailableAsync(definition.Name, userId))
            {
                result.Add(definition);
            }
        }

        return result;
    }

    protected virtual IDictionary<string, NotificationDefinition> CreateDefinitions()
    {
        var context = new NotificationDefinitionContext();

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            foreach (var providerType in Registration.DefinitionProviders.Distinct())
            {
                context.SetCurrentProvider(providerType);
                var provider = (INotificationDefinitionProvider)scope.ServiceProvider.GetRequiredService(providerType);
                provider.Define(context);
            }
        }

        return context.Definitions;
    }
}
