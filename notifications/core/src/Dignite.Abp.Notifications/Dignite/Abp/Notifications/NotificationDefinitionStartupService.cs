using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Validates <see cref="NotificationDefinitionRegistration"/> and materializes notification definitions (plus the
/// data-type registry, via constructor injection) in the host's starting phase, before any hosted service can
/// publish notifications — the single startup fail-fast hook for both concerns, since the real definition-name
/// conflict check only runs lazily inside <see cref="NotificationDefinitionManager"/> and can't be forced from
/// the options-validation pipeline without it resolving itself. It also reconciles <see cref="NotificationRoutingOptions"/>
/// against the definition table and the hosted notifiers, which are only reachable here.
/// </summary>
internal sealed class NotificationDefinitionStartupService : IHostedLifecycleService
{
    private readonly INotificationDefinitionManager _definitionManager;
    private readonly IOptions<NotificationDefinitionRegistration> _definitionRegistration;
    private readonly IOptions<NotificationRoutingOptions> _routingOptions;
    private readonly IOptions<NotificationNotifierOptions> _notifierOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationDefinitionStartupService> _logger;

    public NotificationDefinitionStartupService(
        INotificationDefinitionManager definitionManager,
        IOptions<NotificationDefinitionRegistration> definitionRegistration,
        INotificationDataTypeRegistry dataTypeRegistry,
        IOptions<NotificationRoutingOptions> routingOptions,
        IOptions<NotificationNotifierOptions> notifierOptions,
        IServiceProvider serviceProvider,
        ILogger<NotificationDefinitionStartupService> logger)
    {
        _definitionManager = definitionManager;
        _definitionRegistration = definitionRegistration;
        _routingOptions = routingOptions;
        _notifierOptions = notifierOptions;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _ = dataTypeRegistry;
    }

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        _definitionRegistration.Value.Validate();
        var definitions = _definitionManager.GetAll();

        ValidateRoutingNames(definitions);
        ValidateHostedChannels();
        await ValidateStatelessRoutingAsync(definitions, cancellationToken);
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

    /// <summary>
    /// A channel the rules name but this process does not host is legitimate in a split deployment, so it warns by
    /// default; <see cref="NotificationRoutingOptions.RequireHostedChannels"/> turns it into a startup failure.
    /// </summary>
    private void ValidateHostedChannels()
    {
        var options = _routingOptions.Value;
        var hosted = _notifierOptions.Value.Notifiers;

        var unhosted = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (notificationName, channels) in options.Notifications)
        {
            foreach (var channel in channels.Where(channel => !hosted.ContainsKey(channel.Trim())))
            {
                AddReference(unhosted, channel.Trim(), notificationName);
            }
        }

        foreach (var channel in (options.Default ?? Array.Empty<string>()).Where(channel => !hosted.ContainsKey(channel.Trim())))
        {
            AddReference(unhosted, channel.Trim(), "(default)");
        }

        if (unhosted.Count == 0)
        {
            return;
        }

        if (options.RequireHostedChannels)
        {
            var details = string.Join("; ", unhosted.Select(pair => $"'{pair.Key}' (used by {string.Join(", ", pair.Value)})"));
            throw new AbpException(
                $"{nameof(NotificationRoutingOptions)} names channels that no notifier in this process hosts: {details}. " +
                $"Install the channel's module, or set {nameof(NotificationRoutingOptions.RequireHostedChannels)} to false " +
                "if another process delivers it.");
        }

        foreach (var (channel, notifications) in unhosted)
        {
            _logger.LogWarning(
                "Notification channel {Channel} is not hosted by this process; deliveries routed to it by {Notifications} " +
                "are ignored here. Install the channel's module, or ignore this if another process delivers it.",
                channel,
                string.Join(", ", notifications));
        }
    }

    /// <summary>
    /// Stateless mode has no inbox to fall back on, so a definition that resolves to no channel could never be
    /// delivered anywhere. Only the default resolver can be evaluated here; a replacement is dynamic, and the
    /// distributor's own check covers it at runtime.
    /// </summary>
    private async Task ValidateStatelessRoutingAsync(
        IReadOnlyList<NotificationDefinition> definitions,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        if (scope.ServiceProvider.GetRequiredService<INotificationStore>() is not NullNotificationStore)
        {
            return;
        }

        var resolver = scope.ServiceProvider.GetRequiredService<INotificationChannelResolver>();
        if (resolver is not DefaultNotificationChannelResolver)
        {
            return;
        }

        var unrouted = new List<string>();
        foreach (var definition in definitions)
        {
            var channels = await resolver.ResolveAsync(
                definition,
                new NotificationInfo { NotificationName = definition.Name },
                cancellationToken);

            if (channels == null || channels.Length == 0)
            {
                unrouted.Add(definition.Name);
            }
        }

        if (unrouted.Count > 0)
        {
            throw new AbpException(
                "No NotificationCenter inbox store is installed and these notifications resolve to no external " +
                $"channel: {string.Join(", ", unrouted.Select(name => $"'{name}'"))}. " +
                $"Configure {nameof(NotificationRoutingOptions)} (a rule or Default) or install NotificationCenter.");
        }
    }

    private static void AddReference(
        SortedDictionary<string, SortedSet<string>> references,
        string channel,
        string notificationName)
    {
        if (!references.TryGetValue(channel, out var names))
        {
            references[channel] = names = new SortedSet<string>(StringComparer.Ordinal);
        }

        names.Add(notificationName);
    }
}
