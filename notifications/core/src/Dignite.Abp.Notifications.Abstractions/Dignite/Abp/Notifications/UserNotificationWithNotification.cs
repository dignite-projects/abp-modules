namespace Dignite.Abp.Notifications;

/// <summary>Query result pairing a per-user notification row with the notification it points to.</summary>
public class UserNotificationWithNotification
{
    public UserNotificationInfo UserNotification { get; set; } = default!;

    public NotificationInfo Notification { get; set; } = default!;
}
