using System;
using Volo.Abp.BackgroundJobs;

namespace Dignite.Abp.Notifications;

/// <remarks>
/// The job name is fixed rather than derived from the type's full name, so the queue (for example the RabbitMQ queue
/// <c>AbpBackgroundJobs.Dignite.Abp.Notifications.Distribute</c>) is a stable contract of this package. Only a process
/// that installs Distribution registers the job, so only such a process consumes the queue.
/// </remarks>
[Serializable]
[BackgroundJobName("Dignite.Abp.Notifications.Distribute")]
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
