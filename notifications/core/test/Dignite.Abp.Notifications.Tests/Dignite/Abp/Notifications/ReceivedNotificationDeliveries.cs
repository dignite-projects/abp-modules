using System.Collections.Concurrent;

namespace Dignite.Abp.Notifications;

/// <summary>Singleton sink the test notifier appends delivered work items to.</summary>
public class ReceivedNotificationDeliveries
{
    public ConcurrentQueue<NotificationDeliveryRequestedEto> Items { get; } = new();
}
