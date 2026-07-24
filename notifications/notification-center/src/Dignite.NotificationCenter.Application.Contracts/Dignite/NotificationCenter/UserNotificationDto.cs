using System;
using Dignite.Abp.Notifications;
using Volo.Abp.Application.Dtos;

namespace Dignite.NotificationCenter;

public class UserNotificationDto : EntityDto<Guid>
{
    public Guid UserId { get; set; }

    /// <summary>Id of the underlying notification — the key used to mark-as-read / delete.</summary>
    public Guid NotificationId { get; set; }

    public string NotificationName { get; set; } = default!;

    /// <summary>Display name localized for the current reader's culture (not baked in at publish time).</summary>
    public string? NotificationDisplayName { get; set; }

    public NotificationData? Data { get; set; }

    public string? EntityTypeName { get; set; }

    public string? EntityId { get; set; }

    public NotificationSeverity Severity { get; set; }

    public DateTime CreationTime { get; set; }

    public UserNotificationState State { get; set; }
}
