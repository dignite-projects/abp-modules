namespace Dignite.Abp.Notifications;

/// <summary>
/// Maps each delivery channel to the <see cref="INotificationNotifier"/> type that serves it. Every notifier module
/// registers its own channel here, so Core's delivery handler resolves only the notifier a delivery request names
/// instead of constructing every channel's notifier for every event. A channel name and a notifier type each map
/// once; channel names use ordinal, case-insensitive comparison, like the routing they serve.
/// </summary>
public class NotificationNotifierOptions
{
    public NotificationNotifierDictionary Notifiers { get; }

    public NotificationNotifierOptions()
    {
        Notifiers = new NotificationNotifierDictionary();
    }
}
