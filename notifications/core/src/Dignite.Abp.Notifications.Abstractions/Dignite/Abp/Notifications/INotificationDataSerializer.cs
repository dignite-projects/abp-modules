namespace Dignite.Abp.Notifications;

/// <summary>
/// Serializes <see cref="NotificationData"/> to/from JSON using stable discriminators.
/// </summary>
public interface INotificationDataSerializer
{
    string? Serialize(NotificationData? data);

    /// <summary>
    /// Reads JSON tolerantly: an unknown discriminator or a malformed known payload becomes
    /// <see cref="UnsupportedNotificationData"/> instead of throwing.
    /// </summary>
    NotificationData? Deserialize(string? json);
}
