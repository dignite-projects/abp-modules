using System;

namespace Dignite.Abp.Notifications;

[Serializable]
public class NotificationDistributionJobArgs
{
    public NotificationInfo Notification { get; set; } = default!;

    /// <summary>
    /// <see langword="null"/> selects subscription resolution; an empty array is an intentional no-op; a
    /// non-empty array contains explicit recipients.
    /// </summary>
    public Guid[]? UserIds { get; set; }

    public Guid[]? ExcludedUserIds { get; set; }

    public NotificationDistributionJobArgs()
    {
    }

    public NotificationDistributionJobArgs(NotificationInfo notification, Guid[]? userIds, Guid[]? excludedUserIds)
    {
        Notification = notification;
        UserIds = userIds;
        ExcludedUserIds = excludedUserIds;
    }
}
