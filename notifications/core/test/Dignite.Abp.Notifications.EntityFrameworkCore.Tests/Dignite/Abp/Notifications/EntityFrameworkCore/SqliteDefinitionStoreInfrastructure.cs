using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dignite.Abp.Notifications.EntityFrameworkCore;

/// <summary>
/// A test deployment whose definition tables live in one in-memory SQLite database, open for as long as the test runs.
/// </summary>
public sealed class SqliteDefinitionStoreInfrastructure : SharedDefinitionStoreInfrastructure
{
    public SqliteConnection Connection { get; }

    public override Type ProviderModuleType => typeof(AbpNotificationsEntityFrameworkCoreTestModule);

    public SqliteDefinitionStoreInfrastructure()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        Connection.Open();

        var options = new DbContextOptionsBuilder<NotificationDefinitionStoreDbContext>()
            .UseSqlite(Connection)
            .Options;

        using var context = new NotificationDefinitionStoreDbContext(options);
        context.GetService<IRelationalDatabaseCreator>().CreateTables();
    }

    public override void Dispose()
    {
        Connection.Dispose();
    }
}
