using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Threading;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Best-effort dispatch of a delivery request to the channel notifier hosted by this process. There is no
/// per-recipient delivery state, idempotency, or retry — the notification's authoritative record is the inbox row.
/// </summary>
[ExposeServices(
    typeof(IDistributedEventHandler<NotificationDeliveryRequestedEto>),
    typeof(NotificationDeliveryRequestedHandler))]
public class NotificationDeliveryRequestedHandler :
    IDistributedEventHandler<NotificationDeliveryRequestedEto>,
    ITransientDependency
{
    protected IReadOnlyList<INotificationNotifier> Notifiers { get; }
    protected ICancellationTokenProvider CancellationTokenProvider { get; }
    protected ILogger<NotificationDeliveryRequestedHandler> Logger { get; }

    public NotificationDeliveryRequestedHandler(
        IEnumerable<INotificationNotifier> notifiers,
        ICancellationTokenProvider cancellationTokenProvider,
        ILogger<NotificationDeliveryRequestedHandler> logger)
    {
        Notifiers = notifiers.ToList();
        CancellationTokenProvider = cancellationTokenProvider;
        Logger = logger;
    }

    public virtual async Task HandleEventAsync(NotificationDeliveryRequestedEto eventData)
    {
        var notifier = ResolveNotifierOrNull(eventData.Channel);
        if (notifier == null)
        {
            // Distributed event subscribers receive every channel's work type. A process that does not host this
            // channel leaves the event untouched.
            Logger.LogDebug(
                "Ignoring notification delivery for notification {NotificationId} because channel {Channel} is not hosted by this process.",
                eventData.NotificationId,
                eventData.Channel);
            return;
        }

        var cancellationToken = CancellationTokenProvider.Token;
        try
        {
            await notifier.DeliverAsync(eventData, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Best-effort delivery: log and move on so one channel failure does not poison the event. No recipient
            // ids, exception messages, or payload fragments (invariants §8); operators correlate by notification id.
            Logger.LogWarning(
                "Notification delivery for notification {NotificationId} on channel {Channel} failed with exception type {ExceptionType}.",
                eventData.NotificationId,
                eventData.Channel,
                exception.GetType().FullName);
        }
    }

    protected virtual INotificationNotifier? ResolveNotifierOrNull(string channel)
    {
        var matches = Notifiers
            .Where(notifier => string.Equals(notifier.Name, channel, StringComparison.OrdinalIgnoreCase))
            .GroupBy(notifier => notifier.GetType())
            .Select(group => group.First())
            .ToList();
        if (matches.Count == 0)
        {
            return null;
        }

        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Multiple notification notifiers are registered for channel '{channel}'.");
        }

        return matches[0];
    }
}
