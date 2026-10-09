namespace Dignite.Abp.Notifications;

/// <summary>
/// Where the definition store's tables (EF Core) or collections (MongoDB, same names) live. The connection string name
/// and the defaults are the Notification Center's (<c>NotificationCenterDbProperties</c>), so a host maps one connection
/// string for the inbox and the definitions; this package does not depend on the Notification Center, so a host that
/// changes the Center's prefix or schema changes these too.
/// </summary>
public static class NotificationDefinitionStoreDbProperties
{
    public const string ConnectionStringName = "NotificationCenter";

    public static string DbTablePrefix { get; set; } = "Notif";

    public static string? DbSchema { get; set; } = null;
}
