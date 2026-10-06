namespace Dignite.NotificationCenter;

public static class PushDeviceConsts
{
    /// <summary>A push provider name, e.g. <c>"Expo"</c>.</summary>
    public const int MaxProviderLength = 32;

    /// <summary>Expo tokens are ~41 characters, FCM ~160, APNs 64; generous headroom for future formats.</summary>
    public const int MaxTokenLength = 1024;

    public const int MaxCultureNameLength = 32;

    public const int MaxSessionIdLength = 128;

    /// <summary>Hex SHA-256, the same length as the subscription identity keys.</summary>
    public const int TokenKeyLength = NotificationCenterConsts.SubscriptionIdentityKeyLength;
}
