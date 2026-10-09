using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Dignite.Abp.Notifications;

/// <summary>
/// What the services of one deployment share: the database that holds the definition records, the distributed cache
/// that holds the hashes and the stamp, and the cache key prefix. Each test gets its own, so every application a test
/// starts sees the same store and nothing else — the prefix also keeps ABP's in-process locks (a static, keyed lock)
/// apart from those of tests running in parallel.
/// </summary>
/// <remarks>
/// A persistence provider's test project derives it: the database is the provider's, and
/// <see cref="ProviderModuleType"/> is the module that puts the store on it. <see cref="DefinitionStoreTestApplication"/>
/// adds that module to every application, so the role modules (<see cref="PublisherATestModule"/> and the others) and
/// the scenarios stay the same for every provider.
/// </remarks>
public abstract class SharedDefinitionStoreInfrastructure : IDisposable
{
    public IDistributedCache Cache { get; } =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    public string KeyPrefix { get; } = Guid.NewGuid().ToString("N");

    /// <summary>The common stamp every dynamic store compares.</summary>
    public string StampKey => KeyPrefix + "_DigniteInMemoryNotificationCacheStamp";

    /// <summary>The hash of an application's last save.</summary>
    public string GetHashKey(string applicationName) => $"{KeyPrefix}_{applicationName}_DigniteNotificationsHash";

    /// <summary>
    /// The provider's test module: it depends on the provider's definition store module and points it at this
    /// deployment's database, which it reads from the <see cref="SharedDefinitionStoreInfrastructure"/> singleton.
    /// </summary>
    public abstract Type ProviderModuleType { get; }

    public virtual void Dispose()
    {
    }
}
