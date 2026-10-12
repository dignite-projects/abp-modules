using System;
using Dignite.Abp.Notifications;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Dignite.NotificationCenter;

/// <summary>A per-user copy of a notification, carrying that user's read/unread state (the inbox row).</summary>
public class UserNotification : BasicAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }

    public virtual Guid UserId { get; protected set; }

    public virtual Guid NotificationId { get; protected set; }

    /// <summary>
    /// Copy of <see cref="Notification.NotificationName"/>, so the inbox can be filtered and counted by definition
    /// (and so by group) with single-table indexed queries on both EF Core and MongoDB. Immutable once written.
    /// </summary>
    public virtual string NotificationName { get; protected set; } = default!;

    public virtual UserNotificationState State { get; protected set; }

    public virtual DateTime CreationTime { get; protected set; }

    protected UserNotification()
    {
    }

    public UserNotification(
        Guid id,
        Guid userId,
        Guid notificationId,
        string notificationName,
        UserNotificationState state,
        DateTime creationTime,
        Guid? tenantId)
        : base(id)
    {
        UserId = userId;
        NotificationId = notificationId;
        NotificationName = Check.NotNullOrWhiteSpace(
            notificationName, nameof(notificationName), NotificationCenterConsts.MaxNotificationNameLength);
        State = state;
        CreationTime = creationTime;
        TenantId = tenantId;
    }

    public virtual void SetState(UserNotificationState state)
    {
        State = state;
    }
}
