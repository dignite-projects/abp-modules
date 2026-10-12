namespace Dignite.Abp.Notifications.EntityFrameworkCore;

/// <summary>Runs the shared scenarios on EF Core + SQLite.</summary>
public class AbpNotificationsDomainModule_Tests :
    AbpNotificationsDomainModule_Tests<SqliteDefinitionStoreInfrastructure>
{
}
