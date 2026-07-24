using System;
using Dignite.Abp.Notifications;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Dignite.NotificationCenter;

/// <summary>
/// A published notification. <see cref="Data"/> holds the serialized <c>NotificationData</c> JSON; the stable
/// type discriminator lives inside that JSON, so no CLR type name / AssemblyQualifiedName is persisted.
/// </summary>
public class Notification : BasicAggregateRoot<Guid>, IMultiTenant, IHasConcurrencyStamp
{
    public virtual string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");

    public virtual Guid? TenantId { get; protected set; }

    public virtual string NotificationName { get; protected set; } = default!;

    public virtual string? Data { get; protected set; }

    public virtual string? EntityTypeName { get; protected set; }

    public virtual string? EntityId { get; protected set; }

    public virtual NotificationSeverity Severity { get; protected set; }

    public virtual DateTime CreationTime { get; protected set; }

    protected Notification()
    {
    }

    public Notification(
        Guid id,
        string notificationName,
        string? data,
        string? entityTypeName,
        string? entityId,
        NotificationSeverity severity,
        DateTime creationTime,
        Guid? tenantId)
        : base(id)
    {
        NotificationName = notificationName;
        Data = data;
        EntityTypeName = entityTypeName;
        EntityId = entityId;
        Severity = severity;
        CreationTime = creationTime;
        TenantId = tenantId;
    }
}
