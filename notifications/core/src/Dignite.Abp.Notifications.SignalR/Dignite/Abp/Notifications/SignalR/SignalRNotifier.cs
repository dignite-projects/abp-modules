using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications.SignalR;

/// <summary>
/// Relays single-recipient delivery requests to connected SignalR users. Recipients receive only a
/// <see cref="NotificationPayload"/>, which by construction omits every aggregate recipient list.
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

    protected INotificationDataSerializer DataSerializer { get; }

    public string Name => ChannelName;

    public SignalRNotifier(
        IHubContext<NotificationsHub> hubContext,
        INotificationDataSerializer dataSerializer)
    {
        HubContext = hubContext;
        DataSerializer = dataSerializer;
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
            new object[] { NotificationPayload.FromRequest(request, DataSerializer) },
            cancellationToken);
    }
}
