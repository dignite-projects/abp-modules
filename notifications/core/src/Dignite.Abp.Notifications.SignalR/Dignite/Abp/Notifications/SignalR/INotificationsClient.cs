using System.Threading.Tasks;

namespace Dignite.Abp.Notifications.SignalR;

/// <summary>The strongly-typed client method invoked to deliver a notification payload.</summary>
public interface INotificationsClient
{
    Task ReceiveNotification(NotificationPayload notification);
}
