using System;
using Dignite.Abp.Notifications.DefinitionStore.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>
/// What the services of one deployment share: the database that holds the definition tables, the distributed cache
/// that holds the hashes and the stamp, and the cache key prefix. Each test gets its own, so every application a test
/// starts sees the same store and nothing else — the prefix also keeps ABP's in-process locks (a static, keyed lock)
/// apart from those of tests running in parallel.
/// </summary>
public sealed class SharedDefinitionStoreInfrastructure : IDisposable
{
    public SqliteConnection Connection { get; }

    public IDistributedCache Cache { get; }

    public string KeyPrefix { get; } = Guid.NewGuid().ToString("N");

    /// <summary>The common stamp every dynamic store compares.</summary>
    public string StampKey => KeyPrefix + "_DigniteInMemoryNotificationCacheStamp";

    /// <summary>The hash of an application's last save.</summary>
    public string GetHashKey(string applicationName) => $"{KeyPrefix}_{applicationName}_DigniteNotificationsHash";

    public SharedDefinitionStoreInfrastructure()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        Connection.Open();

        var options = new DbContextOptionsBuilder<NotificationDefinitionStoreDbContext>()
            .UseSqlite(Connection)
            .Options;

        using (var context = new NotificationDefinitionStoreDbContext(options))
        {
            context.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        Cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    }

    public void Dispose()
    {
        Connection.Dispose();
    }
}
