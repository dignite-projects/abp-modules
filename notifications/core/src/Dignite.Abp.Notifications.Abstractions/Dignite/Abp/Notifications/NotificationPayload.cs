using System;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The per-user view of a notification pushed to a client (e.g. over SignalR). It deliberately carries no
/// aggregate recipient list, so a user never receives other users' ids.
/// </summary>
public class NotificationPayload
{
    public Guid NotificationId { get; set; }

    public string NotificationName { get; set; } = default!;

    public NotificationData? Data { get; set; }

    public NotificationSeverity Severity { get; set; }

    public DateTime CreationTime { get; set; }

    /// <summary>
    /// Stable name of the entity type this notification is about, e.g. <c>"Demo.Order"</c> — never a CLR type name.
    /// Null when the notification is not about a specific entity.
    /// </summary>
    public string? EntityTypeName { get; set; }

    /// <summary>The entity's identifier, rendered as a string. Null when <see cref="EntityTypeName"/> is null.</summary>
    public string? EntityId { get; set; }

    public NotificationPayload()
    {
    }

    /// <summary>
    /// Builds the per-user view from a delivery request, hydrating <see cref="Data"/> from the request's
    /// pre-serialized <see cref="NotificationDeliveryRequestedEto.DataJson"/> envelope. The read is tolerant:
    /// an unknown or malformed payload becomes <see cref="UnsupportedNotificationData"/> instead of throwing.
    /// </summary>
    public static NotificationPayload FromRequest(
        NotificationDeliveryRequestedEto request,
        INotificationDataSerializer dataSerializer)
    {
        return new NotificationPayload
        {
            NotificationId = request.NotificationId,
            NotificationName = request.NotificationName,
            Data = dataSerializer.Deserialize(request.DataJson),
            Severity = request.Severity,
            CreationTime = request.CreationTime,
            EntityTypeName = request.EntityTypeName,
            EntityId = request.EntityId
        };
    }
}
