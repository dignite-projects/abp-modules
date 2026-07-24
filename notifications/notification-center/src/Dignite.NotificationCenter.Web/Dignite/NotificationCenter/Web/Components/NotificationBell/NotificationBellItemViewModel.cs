namespace Dignite.NotificationCenter.Web.Components.NotificationBell;

public class NotificationBellItemViewModel
{
    public UserNotificationDto Notification { get; }

    /// <summary>The stable <see cref="Dignite.Abp.Notifications.NotificationDataTypeAttribute"/> discriminator
    /// for <see cref="Notification"/>'s <c>Data</c>, or <c>null</c> if it has none (e.g. <c>Data</c> is null).</summary>
    public string? Discriminator { get; }

    /// <summary>Resolved via <see cref="NotificationCenterWebOptions.EntityLinkResolvers"/>; null if no
    /// resolver is registered for this item's <c>EntityTypeName</c> (not every notification is navigable).</summary>
    public string? Url { get; }

    public NotificationBellItemViewModel(UserNotificationDto notification, string? discriminator, string? url)
    {
        Notification = notification;
        Discriminator = discriminator;
        Url = url;
    }
}
