namespace Dignite.NotificationCenter;

public static class NotificationCenterDbProperties
{
    public const string ConnectionStringName = "NotificationCenter";

    public static string DbTablePrefix { get; set; } = "Notif";

    public static string? DbSchema { get; set; } = null;
}
