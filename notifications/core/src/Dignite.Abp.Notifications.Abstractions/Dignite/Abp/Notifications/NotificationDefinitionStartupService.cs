using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Volo.Abp;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Validates <see cref="NotificationDefinitionRegistration"/> and materializes notification definitions (plus the
/// data-type registry, via constructor injection) in the host's starting phase, before any hosted service can
/// publish notifications — the single startup fail-fast hook for both concerns, since the real definition-name
/// conflict check only runs lazily inside <see cref="IStaticNotificationDefinitionStore"/> and can't be forced from
/// the options-validation pipeline without it resolving itself. It also reconciles the rule names of
/// <see cref="NotificationRoutingOptions"/> against the definition table, which is only reachable here.
/// </summary>
/// <remarks>
/// The checks that depend on what the process delivers — channels no notifier here hosts, and stateless mode — belong to
/// the process that delivers, so they run in the implementation package, Dignite.Abp.Notifications. A publisher whose notifications are
/// delivered elsewhere still gets the routing-name check: a typo in its rules is a typo wherever it is delivered.
/// Only this process's own (static) definitions count: its rules route what its modules define, and the check must not
/// depend on what other processes have saved to a definition store by the time this one starts.
/// </remarks>
internal sealed class NotificationDefinitionStartupService : IHostedLifecycleService
{
    private readonly IStaticNotificationDefinitionStore _staticStore;
    private readonly IOptions<NotificationDefinitionRegistration> _definitionRegistration;
    private readonly IOptions<NotificationRoutingOptions> _routingOptions;

    public NotificationDefinitionStartupService(
        IStaticNotificationDefinitionStore staticStore,
        IOptions<NotificationDefinitionRegistration> definitionRegistration,
        INotificationDataTypeRegistry dataTypeRegistry,
        IOptions<NotificationRoutingOptions> routingOptions)
    {
        _staticStore = staticStore;
        _definitionRegistration = definitionRegistration;
        _routingOptions = routingOptions;
        _ = dataTypeRegistry;
    }

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        _definitionRegistration.Value.Validate();
        ValidateRoutingNames(await _staticStore.GetNotificationsAsync());
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>A rule naming a notification that no definition provider registered is a typo, not a no-op.</summary>
    private void ValidateRoutingNames(IReadOnlyList<NotificationDefinition> definitions)
    {
        var known = new HashSet<string>(definitions.Select(definition => definition.Name), StringComparer.Ordinal);
        var unknown = _routingOptions.Value.Notifications.Keys
            .Where(name => !known.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (unknown.Length > 0)
        {
            throw new AbpException(
                $"{nameof(NotificationRoutingOptions)} has rules for notifications that are not defined: " +
                $"{string.Join(", ", unknown.Select(name => $"'{name}'"))}. " +
                "Check the spelling, and that the module defining them is part of this application.");
        }
    }
}
