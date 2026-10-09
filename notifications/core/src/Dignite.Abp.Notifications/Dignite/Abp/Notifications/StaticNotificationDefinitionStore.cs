using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Builds the definitions of this process's providers once, on first use, and keeps them for the life of the
/// application. Mirrors ABP's <c>StaticFeatureDefinitionStore</c>: a singleton holding definition-time metadata only —
/// it runs each provider type once in a scope of its own and captures no request-scoped service.
/// </summary>
public class StaticNotificationDefinitionStore : IStaticNotificationDefinitionStore, ISingletonDependency
{
    protected NotificationDefinitionRegistration Registration { get; }

    protected IServiceScopeFactory ServiceScopeFactory { get; }

    private readonly Lazy<DefinitionSnapshot> _snapshot;

    public StaticNotificationDefinitionStore(
        IOptions<NotificationDefinitionRegistration> registration,
        IServiceScopeFactory serviceScopeFactory)
    {
        Registration = registration.Value;
        ServiceScopeFactory = serviceScopeFactory;
        _snapshot = new Lazy<DefinitionSnapshot>(() => new DefinitionSnapshot(CreateGroups()), isThreadSafe: true);
    }

    public virtual Task<NotificationDefinition?> GetOrNullAsync(string name)
    {
        return Task.FromResult(_snapshot.Value.Definitions.TryGetValue(name, out var definition) ? definition : null);
    }

    public virtual Task<IReadOnlyList<NotificationDefinition>> GetNotificationsAsync()
    {
        return Task.FromResult(_snapshot.Value.OrderedDefinitions);
    }

    public virtual Task<IReadOnlyList<NotificationGroupDefinition>> GetGroupsAsync()
    {
        return Task.FromResult(_snapshot.Value.Groups);
    }

    /// <summary>
    /// Builds the groups (and through them, the definitions) once. Overrides can populate their own
    /// <see cref="NotificationDefinitionContext"/> and return its <see cref="NotificationDefinitionContext.Groups"/>.
    /// A name registered twice throws here, which startup turns into a failed start.
    /// </summary>
    protected virtual IReadOnlyList<NotificationGroupDefinition> CreateGroups()
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

        return context.Groups;
    }

    private sealed class DefinitionSnapshot
    {
        public IReadOnlyList<NotificationGroupDefinition> Groups { get; }

        public IReadOnlyList<NotificationDefinition> OrderedDefinitions { get; }

        public IReadOnlyDictionary<string, NotificationDefinition> Definitions { get; }

        public DefinitionSnapshot(IReadOnlyList<NotificationGroupDefinition> groups)
        {
            Groups = groups.ToList();
            OrderedDefinitions = Groups.SelectMany(group => group.Notifications).ToList();
            Definitions = OrderedDefinitions.ToDictionary(definition => definition.Name, StringComparer.Ordinal);
        }
    }
}
