namespace Dignite.Abp.Notifications.Push;

/// <summary>Options used when a device registration does not carry a culture.</summary>
public class NotificationPushOptions
{
    /// <summary>The culture assumed when nothing else supplies one.</summary>
    public const string DefaultCultureName = "en";

    /// <summary>Default BCP-47 culture used to build push content.</summary>
    public string DefaultCulture { get; set; } = DefaultCultureName;
}
