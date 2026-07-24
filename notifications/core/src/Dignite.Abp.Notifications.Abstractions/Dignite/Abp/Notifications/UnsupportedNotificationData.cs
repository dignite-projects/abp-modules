namespace Dignite.Abp.Notifications;

/// <summary>
/// Safe tolerant-read placeholder. It preserves the original stable metadata and JSON for diagnostics without
/// resolving or activating an unknown CLR type. APIs/UIs should render a generic unsupported-notification fallback.
/// </summary>
[NotificationDataType(Discriminator)]
public sealed class UnsupportedNotificationData : NotificationData
{
    public const string Discriminator = "Dignite.Unsupported";

    public string? OriginalDiscriminator { get; set; }

    public UnsupportedNotificationDataReason Reason { get; set; }

    /// <summary>
    /// Original JSON text. It is emitted as an escaped JSON string when this placeholder crosses another boundary,
    /// never reparsed as a CLR type.
    /// </summary>
    public string RawJson { get; set; } = string.Empty;
}
