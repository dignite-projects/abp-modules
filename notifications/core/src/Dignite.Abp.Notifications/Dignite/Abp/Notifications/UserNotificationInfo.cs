using System;

namespace Dignite.Abp.Notifications;

public class UserNotificationInfo
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid NotificationId { get; set; }

    public UserNotificationState State { get; set; } = UserNotificationState.Unread;

    public DateTime CreationTime { get; set; }

    public Guid? TenantId { get; set; }
}
