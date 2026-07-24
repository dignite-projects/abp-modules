using System.Collections.Generic;

namespace Dignite.Abp.Notifications;

/// <summary>
/// A message localized on the reading side (by resource + key), so each reader sees it in their own culture
/// rather than a culture baked in at publish time.
/// </summary>
[NotificationDataType("Dignite.LocalizableMessage")]
public class LocalizableMessageNotificationData : NotificationData
{
    /// <summary>Localization resource name.</summary>
    public string ResourceName { get; set; } = default!;

    /// <summary>Localization key.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Optional named format arguments for the localized string.</summary>
    public Dictionary<string, object>? Arguments { get; set; }

    public LocalizableMessageNotificationData()
    {
    }

    public LocalizableMessageNotificationData(string resourceName, string name)
    {
        ResourceName = resourceName;
        Name = name;
    }
}
