using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Threading;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Best-effort dispatch of a delivery request to the channel notifier hosted by this process. There is no
/// per-recipient delivery state, idempotency, or retry — the notification's authoritative record is the inbox row.
/// </summary>
/// <remarks>
/// Only the notifier registered for the event's channel in <see cref="NotificationNotifierOptions"/> is constructed,
/// so an email delivery never builds the push or SignalR notifier (or their dependency graphs), and one channel's
/// notifier failing to construct cannot break another channel's deliveries.
/// </remarks>
[ExposeServices(
    typeof(IDistributedEventHandler<NotificationDeliveryRequestedEto>),
    typeof(NotificationDeliveryRequestedHandler))]
public class NotificationDeliveryRequestedHandler :
    IDistributedEventHandler<NotificationDeliveryRequestedEto>,
    ITransientDependency
{
    protected IServiceProvider ServiceProvider { get; }
    protected IOptions<NotificationNotifierOptions> NotifierOptions { get; }
    protected ICancellationTokenProvider CancellationTokenProvider { get; }
    protected ILogger<NotificationDeliveryRequestedHandler> Logger { get; }

    public NotificationDeliveryRequestedHandler(
        IServiceProvider serviceProvider,
        IOptions<NotificationNotifierOptions> notifierOptions,
        ICancellationTokenProvider cancellationTokenProvider,
        ILogger<NotificationDeliveryRequestedHandler> logger)
    {
        ServiceProvider = serviceProvider;
        NotifierOptions = notifierOptions;
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
        // The event bus resolves this handler from a per-event scope, so the notifier shares that scope's lifetime.
        // Two types claiming one channel never reach here: the options reject them as they are added.
        if (!NotifierOptions.Value.Notifiers.TryGetValue(channel, out var notifierType))
        {
            return null;
        }

        var notifier = (INotificationNotifier)ServiceProvider.GetRequiredService(notifierType);
        if (!string.Equals(notifier.Name, channel, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Notification notifier '{notifierType.FullName}' is registered for channel '{channel}' but its " +
                $"{nameof(INotificationNotifier.Name)} is '{notifier.Name}'.");
        }

        return notifier;
    }
}
