using System.Text.Json.Serialization;

namespace Dignite.Abp.Notifications;

/// <summary>Machine-readable reason a payload could not be materialized as its registered current CLR model.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnsupportedNotificationDataReason
{
    UnknownDiscriminator = 0,
    MalformedPayload = 1
}
