using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications.SignalR;

/// <summary>
/// Relays single-recipient delivery requests to connected SignalR users. Recipients receive only a
/// <see cref="SignalRNotificationMessage"/> — a flat wire DTO that omits every aggregate recipient list and
/// carries the notification data as raw JSON, because hub protocols do not use this module's
/// polymorphic <see cref="NotificationData"/> converter.
/// </summary>
[ExposeServices(
    typeof(INotificationNotifier),
    typeof(SignalRNotifier))]
public class SignalRNotifier :
    INotificationNotifier,
    ITransientDependency
{
    public const string ChannelName = "SignalR";

    protected IHubContext<NotificationsHub> HubContext { get; }

    public string Name => ChannelName;

    public SignalRNotifier(IHubContext<NotificationsHub> hubContext)
    {
        HubContext = hubContext;
    }

    public virtual async Task DeliverAsync(
        NotificationDeliveryRequestedEto request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.Channel, Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The {nameof(SignalRNotifier)} cannot deliver channel '{request.Channel}'.");
        }

        await HubContext.Clients.User(request.UserId.ToString()).SendCoreAsync(
            nameof(INotificationsClient.ReceiveNotification),
            new object[] { SignalRNotificationMessage.FromRequest(request) },
            cancellationToken);
    }
}
