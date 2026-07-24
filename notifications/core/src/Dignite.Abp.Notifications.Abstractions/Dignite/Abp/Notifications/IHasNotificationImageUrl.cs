namespace Dignite.Abp.Notifications;

/// <summary>
/// Optional capability a <see cref="NotificationData"/> subclass can implement to have its image shown by a
/// generic/default notification list renderer (e.g. <c>NotificationCenter.Web</c>'s default card), without
/// forcing every notification type to carry an unused image field on the shared base class.
/// </summary>
public interface IHasNotificationImageUrl
{
    string? ImageUrl { get; }
}
