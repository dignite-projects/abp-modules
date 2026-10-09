namespace Dignite.Abp.Notifications;

/// <summary>
/// Column sizes of <c>NotificationDefinitionRecord</c> (Dignite.Abp.Notifications.Domain), as ABP's
/// <c>FeatureDefinitionRecordConsts</c>.
/// </summary>
public static class NotificationDefinitionRecordConsts
{
    /// <summary>Default value: 256, the Notification Center's notification name length.</summary>
    public static int MaxNameLength { get; set; } = 256;

    /// <summary>Default value: 256. Holds a serialized display name (<c>L:Resource,Key</c> or <c>F:text</c>).</summary>
    public static int MaxDisplayNameLength { get; set; } = 256;

    /// <summary>Default value: 256. Holds a serialized description.</summary>
    public static int MaxDescriptionLength { get; set; } = 256;

    /// <summary>Default value: 128, ABP's permission name length.</summary>
    public static int MaxPermissionNameLength { get; set; } = 128;

    /// <summary>Default value: 128, ABP's feature name length.</summary>
    public static int MaxFeatureNameLength { get; set; } = 128;
}
