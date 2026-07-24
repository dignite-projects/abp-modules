namespace Dignite.NotificationCenter;

public static class NotificationCenterPermissions
{
    public const string GroupName = "NotificationCenter";

    public static class Deliveries
    {
        public const string Default = GroupName + ".Deliveries";

        public const string Retry = Default + ".Retry";

        public const string ForceDeliver = Default + ".ForceDeliver";
    }
}
