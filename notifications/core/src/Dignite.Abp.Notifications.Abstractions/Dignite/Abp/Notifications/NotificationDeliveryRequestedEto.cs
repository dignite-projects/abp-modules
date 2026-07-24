using System;
using Volo.Abp.EventBus;

namespace Dignite.Abp.Notifications;

/// <summary>
/// A best-effort delivery request for exactly one tenant, notification, recipient and channel. A channel
/// notifier delivers it once and does not persist per-recipient delivery state.
/// </summary>
/// <remarks>
/// This ETO is a transport-level wire contract: ABP's event bus (and its outbox/inbox) serializes it with
/// plain System.Text.Json, without the application's <c>AbpSystemTextJsonSerializerOptions</c>. Every member
/// must therefore stay default-STJ round-trippable — no abstract/polymorphic properties.
/// </remarks>
[EventName("Dignite.Abp.Notifications.NotificationDeliveryRequested")]
[Serializable]
public class NotificationDeliveryRequestedEto : IEventDataMayHaveTenantId
{
    public Guid NotificationId { get; set; }

    public string NotificationName { get; set; } = default!;

    /// <summary>
    /// The notification payload as discriminator-tagged JSON produced by <see cref="INotificationDataSerializer"/>
    /// (e.g. <c>{"type":"Dignite.Message","message":"..."}</c>), or null when the notification carries no data.
    /// Kept pre-serialized so the ETO survives any transport serializer and stays readable for non-.NET consumers;
    /// consumers hydrate it via <see cref="INotificationDataSerializer.Deserialize"/> (tolerant read).
    /// </summary>
    public string? DataJson { get; set; }

    public NotificationSeverity Severity { get; set; }

    public DateTime CreationTime { get; set; }

    public Guid UserId { get; set; }

    public string Channel { get; set; } = default!;

    public string? EntityTypeName { get; set; }

    public string? EntityId { get; set; }

    public Guid? TenantId { get; set; }

    public bool IsMultiTenant(out Guid? tenantId)
    {
        tenantId = TenantId;
        return TenantId.HasValue;
    }
}
