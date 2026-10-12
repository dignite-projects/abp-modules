namespace Dignite.NotificationCenter;

/// <summary>A notification group as shown in the current user's inbox (e.g. a tab), with its unread count.</summary>
public class UserNotificationGroupDto
{
    /// <summary>
    /// The group's stable name — pass it as <see cref="GetUserNotificationListInput.GroupName"/>.
    /// <see cref="NotificationCenterConsts.OtherGroupName"/> is the synthetic bucket for notifications whose
    /// definition no longer exists.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>Display name localized for the current reader's culture.</summary>
    public string DisplayName { get; set; } = default!;

    public int UnreadCount { get; set; }
}
