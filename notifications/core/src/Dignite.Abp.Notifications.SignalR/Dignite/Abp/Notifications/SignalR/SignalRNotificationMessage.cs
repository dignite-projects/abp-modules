using System;
using System.Text.Json;

namespace Dignite.Abp.Notifications.SignalR;

/// <summary>
/// The wire contract pushed to SignalR clients through <see cref="INotificationsClient.ReceiveNotification"/>.
/// It carries no aggregate recipient list, so a user never receives other users' ids.
/// </summary>
/// <remarks>
/// SignalR hub protocols serialize with their own plain System.Text.Json options, which know nothing about this
/// module's <c>NotificationDataJsonConverter</c>; a polymorphic <see cref="NotificationData"/> member would
/// therefore reach clients as <c>{}</c>. <see cref="Data"/> is consequently the raw discriminator-tagged JSON object
/// (e.g. <c>{"type":"Dignite.Message","message":"..."}</c>) — the same shape the REST inbox returns for
/// <c>UserNotificationDto.Data</c>. Every member stays default-STJ round-trippable.
/// The MessagePack hub protocol is not supported: <see cref="JsonElement"/> has no MessagePack formatter.
/// </remarks>
public class SignalRNotificationMessage
{
    public Guid NotificationId { get; set; }

    public string NotificationName { get; set; } = default!;

    /// <summary>
    /// The notification payload as a discriminator-tagged JSON object, or null when the notification carries
    /// no data (or its payload could not be parsed).
    /// </summary>
    public JsonElement? Data { get; set; }

    public NotificationSeverity Severity { get; set; }

    public DateTime CreationTime { get; set; }

    /// <summary>
    /// Stable name of the entity type this notification is about, e.g. <c>"Demo.Order"</c> — never a CLR type name.
    /// Null when the notification is not about a specific entity.
    /// </summary>
    public string? EntityTypeName { get; set; }

    /// <summary>The entity's identifier, rendered as a string. Null when <see cref="EntityTypeName"/> is null.</summary>
    public string? EntityId { get; set; }

    public SignalRNotificationMessage()
    {
    }

    /// <summary>
    /// Builds the wire message from a delivery request, passing the request's pre-serialized
    /// <see cref="NotificationDeliveryRequestedEto.DataJson"/> through as a raw JSON object. The payload is
    /// not interpreted: an unknown discriminator reaches the client untouched and the client decides.
    /// </summary>
    public static SignalRNotificationMessage FromRequest(NotificationDeliveryRequestedEto request)
    {
        return new SignalRNotificationMessage
        {
            NotificationId = request.NotificationId,
            NotificationName = request.NotificationName,
            Data = ParseData(request.DataJson),
            Severity = request.Severity,
            CreationTime = request.CreationTime,
            EntityTypeName = request.EntityTypeName,
            EntityId = request.EntityId
        };
    }

    private static JsonElement? ParseData(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(dataJson);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Best-effort delivery, matching the tolerant-read philosophy: a malformed payload must not
            // stop the notification envelope from reaching the client.
            return null;
        }
    }
}
