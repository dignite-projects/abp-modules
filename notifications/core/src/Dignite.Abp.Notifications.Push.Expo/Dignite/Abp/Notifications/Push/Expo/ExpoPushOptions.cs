namespace Dignite.Abp.Notifications.Push.Expo;

public class ExpoPushOptions
{
    public const string DefaultBaseAddress = "https://exp.host";

    /// <summary>
    /// The Expo push service origin. Only tests and proxies change this; a path (a proxy prefix) is kept.
    /// </summary>
    public string BaseAddress { get; set; } = DefaultBaseAddress;

    /// <summary>
    /// An Expo access token, sent as a bearer token. Required once "Enhanced Security for Push Notifications" is on
    /// for the Expo project — and turn it on in production: without it, anyone holding a device's Expo push token
    /// can push to that device. Read it from secret configuration, never from source control.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>The sound played on arrival. <c>"default"</c> is the system sound; null is silent.</summary>
    public string? Sound { get; set; } = "default";

    /// <summary><c>"default"</c>, <c>"normal"</c> or <c>"high"</c>. High wakes the device on Android.</summary>
    public string Priority { get; set; } = "high";

    /// <summary>The Android notification channel the app created, or null for Expo's default channel.</summary>
    public string? AndroidChannelId { get; set; }
}
