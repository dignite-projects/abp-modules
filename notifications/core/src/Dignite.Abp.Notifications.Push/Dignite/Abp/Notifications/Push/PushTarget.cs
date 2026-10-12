using Volo.Abp;

namespace Dignite.Abp.Notifications.Push;

/// <summary>One device a user can be pushed to.</summary>
public sealed class PushTarget
{
    /// <summary>The <see cref="IPushProvider.Name"/> that issued <see cref="Token"/>, e.g. <c>"Expo"</c>.</summary>
    public string Provider { get; }

    /// <summary>The provider-issued device token. Treat it as a secret: never log it or put it in a URL.</summary>
    public string Token { get; }

    /// <summary>
    /// Optional BCP-47 culture name the device's content is built in (the app's language when it registered). Null
    /// falls back to <see cref="NotificationPushOptions.DefaultCulture"/>.
    /// </summary>
    public string? CultureName { get; }

    public PushTarget(string provider, string token, string? cultureName = null)
    {
        Provider = Check.NotNullOrWhiteSpace(provider, nameof(provider));
        Token = Check.NotNullOrWhiteSpace(token, nameof(token));
        CultureName = string.IsNullOrWhiteSpace(cultureName) ? null : cultureName;
    }
}
