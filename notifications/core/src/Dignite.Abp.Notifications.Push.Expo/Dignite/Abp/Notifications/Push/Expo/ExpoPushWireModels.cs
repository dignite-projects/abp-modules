using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Dignite.Abp.Notifications.Push.Expo;

// The Expo push API's wire shapes (https://docs.expo.dev/push-notifications/sending-notifications/). Internal: they
// are this provider's implementation detail, not a contract other packages build on.

internal sealed class ExpoPushRequestMessage
{
    [JsonPropertyName("to")]
    public string To { get; set; } = default!;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; } = default!;

    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, string>? Data { get; set; }

    [JsonPropertyName("sound")]
    public string? Sound { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("channelId")]
    public string? ChannelId { get; set; }
}

internal sealed class ExpoPushResponse
{
    [JsonPropertyName("data")]
    public List<ExpoPushTicket>? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<ExpoPushRequestError>? Errors { get; set; }
}

internal sealed class ExpoPushTicket
{
    /// <summary><c>"ok"</c> or <c>"error"</c>.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("details")]
    public ExpoPushTicketDetails? Details { get; set; }
}

internal sealed class ExpoPushTicketDetails
{
    /// <summary>E.g. <c>"DeviceNotRegistered"</c>, <c>"MessageTooBig"</c>, <c>"MessageRateExceeded"</c>.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

internal sealed class ExpoPushRequestError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
