using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Dignite.NotificationCenter;

/// <summary>A user's subscription to a notification type, optionally scoped to a specific entity.</summary>
public class NotificationSubscription : BasicAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }

    /// <summary>Non-null persistence key for <see cref="TenantId"/>; host scope uses <see cref="Guid.Empty"/>.</summary>
    public virtual Guid TenantKey { get; protected set; }

    public virtual Guid UserId { get; protected set; }

    public virtual string NotificationName { get; protected set; } = default!;

    /// <summary>Ordinal SHA-256 key of <see cref="NotificationName"/> used by provider-neutral indexes.</summary>
    public virtual string NotificationNameKey { get; protected set; } = default!;

    public virtual string? EntityTypeName { get; protected set; }

    public virtual string? EntityId { get; protected set; }

    /// <summary>Ordinal SHA-256 key of the definition-wide or entity-specific scope.</summary>
    public virtual string ScopeKey { get; protected set; } = default!;

    public virtual DateTime CreationTime { get; protected set; }

    protected NotificationSubscription()
    {
    }

    public NotificationSubscription(
        Guid id,
        Guid userId,
        string notificationName,
        string? entityTypeName,
        string? entityId,
        DateTime creationTime,
        Guid? tenantId)
        : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A subscription user identifier cannot be Guid.Empty.", nameof(userId));
        }

        UserId = userId;
        NotificationNameKey = NotificationSubscriptionIdentity.GetNotificationNameKey(notificationName);
        NotificationName = notificationName;
        ScopeKey = NotificationSubscriptionIdentity.GetScopeKey(entityTypeName, entityId);
        EntityTypeName = entityTypeName;
        EntityId = entityId;
        CreationTime = creationTime;
        TenantId = tenantId;
        TenantKey = NotificationSubscriptionIdentity.GetTenantKey(tenantId);
    }
}
