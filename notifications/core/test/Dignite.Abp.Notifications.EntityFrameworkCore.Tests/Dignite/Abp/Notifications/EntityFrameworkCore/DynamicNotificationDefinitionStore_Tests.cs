namespace Dignite.Abp.Notifications.EntityFrameworkCore;

/// <summary>Runs the shared scenarios on EF Core + SQLite.</summary>
public class DynamicNotificationDefinitionStore_Tests :
    DynamicNotificationDefinitionStore_Tests<SqliteDefinitionStoreInfrastructure>
{
}
