namespace Dignite.Abp.Notifications.Push;

/// <summary>Orders <see cref="INotificationPushContentProvider"/>s. Lower runs first.</summary>
public static class NotificationPushProviderOrders
{
    /// <summary>Where an application's own provider belongs — ahead of the built-in fallbacks.</summary>
    public const int Default = 0;

    /// <summary>Where this framework's last-resort implementations sit.</summary>
    public const int BuiltInFallback = 1000;
}
