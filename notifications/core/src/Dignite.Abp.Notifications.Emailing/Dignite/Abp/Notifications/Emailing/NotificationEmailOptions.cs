namespace Dignite.Abp.Notifications.Emailing;

/// <summary>
/// Options used when a recipient resolver does not provide a culture.
/// </summary>
public class NotificationEmailOptions
{
    /// <summary>The culture assumed when nothing else supplies one.</summary>
    public const string DefaultCultureName = "en";

    /// <summary>Default BCP-47 culture used to build notification emails.</summary>
    public string DefaultCulture { get; set; } = DefaultCultureName;
}
