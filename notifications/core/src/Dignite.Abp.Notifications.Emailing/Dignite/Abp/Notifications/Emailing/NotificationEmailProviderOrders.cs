namespace Dignite.Abp.Notifications.Emailing;

/// <summary>
/// Shared by both email chains: <see cref="IEmailNotificationAddressResolver"/> (which mailbox) and
/// <see cref="INotificationEmailContentProvider"/> (which body). Lower runs first.
/// </summary>
public static class NotificationEmailProviderOrders
{
    /// <summary>Where an application's own resolver or provider belongs — ahead of the built-in fallbacks.</summary>
    public const int Default = 0;

    /// <summary>Where this framework's last-resort implementations sit.</summary>
    public const int BuiltInFallback = 1000;
}
