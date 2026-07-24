using System;
using Dignite.Abp.Notifications;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Dignite.NotificationCenter;

/// <summary>A per-user copy of a notification, carrying that user's read/unread state (the inbox row).</summary>
public class UserNotification : BasicAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }

    public virtual Guid UserId { get; protected set; }

    public virtual Guid NotificationId { get; protected set; }

    public virtual UserNotificationState State { get; protected set; }

    public virtual DateTime CreationTime { get; protected set; }

    protected UserNotification()
    {
    }

    public UserNotification(
        Guid id,
        Guid userId,
        Guid notificationId,
        UserNotificationState state,
        DateTime creationTime,
        Guid? tenantId)
        : base(id)
    {
        UserId = userId;
        NotificationId = notificationId;
        State = state;
        CreationTime = creationTime;
        TenantId = tenantId;
    }

    public virtual void SetState(UserNotificationState state)
    {
        State = state;
    }
}
