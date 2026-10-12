namespace Dignite.Abp.Notifications.Push;

/// <summary>
/// The keys of the silent data every push message carries, so a tapped notification can open the right inbox entry.
/// </summary>
/// <remarks>
/// Deliberately ids and names only — never the payload (<c>DataJson</c>). A push payload is capped at about 4 KB and
/// travels through Apple's, Google's and the push provider's servers; the app fetches the full notification from the
/// Notification Center API when it needs it. Null values are omitted.
/// </remarks>
public static class PushDataKeys
{
    /// <summary>The key for marking the notification read: <c>POST /api/notification-center/notifications/{id}/mark-as-read</c>.</summary>
    public const string NotificationId = "notificationId";

    public const string NotificationName = "notificationName";

    public const string EntityTypeName = "entityTypeName";

    public const string EntityId = "entityId";
}
