namespace Dignite.NotificationCenter;

public static class NotificationCenterConsts
{
    public const int MaxNotificationNameLength = 256;

    public const int MaxEntityTypeNameLength = 512;

    public const int MaxEntityIdLength = 128;

    public const int SubscriptionIdentityKeyLength = 64;

    /// <summary>
    /// Name of the synthetic inbox group holding notifications whose definition no longer exists. Not a definable
    /// group; namespaced so it cannot collide with a real one.
    /// </summary>
    public const string OtherGroupName = "Dignite.NotificationCenter.Other";
}
