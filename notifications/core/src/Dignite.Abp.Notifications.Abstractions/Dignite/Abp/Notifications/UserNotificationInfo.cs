using System;

namespace Dignite.Abp.Notifications;

public class UserNotificationInfo
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid NotificationId { get; set; }

    /// <summary>
    /// The notification's definition name, copied onto the inbox row so a user's inbox can be filtered and counted by
    /// definition (and through it, by group) without joining the notification payload.
    /// </summary>
    public string NotificationName { get; set; } = default!;

    public UserNotificationState State { get; set; } = UserNotificationState.Unread;

    public DateTime CreationTime { get; set; }

    public Guid? TenantId { get; set; }
}
