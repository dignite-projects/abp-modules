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
/// Reconciles <see cref="NotificationRoutingOptions"/> with what this process delivers, in the host's starting phase:
/// channels the rules name that no notifier here hosts, and — in stateless mode, where there is no inbox — definitions
/// that resolve to no channel at all. Both checks are about the process that distributes, so they live with the
/// distributor; Abstractions' <c>NotificationDefinitionStartupService</c> keeps the check that holds everywhere (rules for
/// notifications nobody defines). Like that check, it reads this process's own definitions
/// (<see cref="IStaticNotificationDefinitionStore"/>): the routing table configured here routes them.
/// </summary>
internal sealed class NotificationDistributionStartupService : IHostedLifecycleService
{
    private readonly IStaticNotificationDefinitionStore _staticStore;
    private readonly IOptions<NotificationRoutingOptions> _routingOptions;
    private readonly IOptions<NotificationNotifierOptions> _notifierOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationDistributionStartupService> _logger;

    public NotificationDistributionStartupService(
        IStaticNotificationDefinitionStore staticStore,
        IOptions<NotificationRoutingOptions> routingOptions,
        IOptions<NotificationNotifierOptions> notifierOptions,
        IServiceProvider serviceProvider,
        ILogger<NotificationDistributionStartupService> logger)
    {
        _staticStore = staticStore;
        _routingOptions = routingOptions;
        _notifierOptions = notifierOptions;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        ValidateHostedChannels();
        await ValidateStatelessRoutingAsync(await _staticStore.GetNotificationsAsync(), cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
