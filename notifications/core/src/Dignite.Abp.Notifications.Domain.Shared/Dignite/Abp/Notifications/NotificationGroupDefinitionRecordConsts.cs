namespace Dignite.Abp.Notifications;

/// <summary>
/// Column sizes of <c>NotificationGroupDefinitionRecord</c> (Dignite.Abp.Notifications.Domain), as ABP's
/// <c>PermissionGroupDefinitionRecordConsts</c>.
/// </summary>
public static class NotificationGroupDefinitionRecordConsts
{
    /// <summary>Default value: 128.</summary>
    public static int MaxNameLength { get; set; } = 128;

    /// <summary>Default value: 256. Holds a serialized display name (<c>L:Resource,Key</c> or <c>F:text</c>).</summary>
    public static int MaxDisplayNameLength { get; set; } = 256;
}
