using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Threading;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>
/// Reads the definitions every process saved, step for step as ABP's <c>DynamicPermissionDefinitionStore</c>: with
/// <see cref="NotificationDefinitionStoreOptions.IsDynamicNotificationStoreEnabled"/> off it has nothing; otherwise,
/// under the cache's semaphore, it compares the common stamp at most every 30 seconds, reloads every record when the
/// stamp changed, and creates the stamp under the common lock when there is none yet.
/// </summary>
/// <remarks>
/// A definition another process has just saved is therefore visible here within about 30 seconds.
/// </remarks>
[Dependency(ReplaceServices = true)]
public class DynamicNotificationDefinitionStore : IDynamicNotificationDefinitionStore, ITransientDependency
{
    protected INotificationGroupDefinitionRecordRepository NotificationGroupRepository { get; }

    protected INotificationDefinitionRecordRepository NotificationRepository { get; }

    protected IDynamicNotificationDefinitionStoreInMemoryCache StoreCache { get; }

    protected IDistributedCache DistributedCache { get; }

    protected IAbpDistributedLock DistributedLock { get; }

    public NotificationDefinitionStoreOptions StoreOptions { get; }

    protected AbpDistributedCacheOptions CacheOptions { get; }

    public DynamicNotificationDefinitionStore(
        INotificationGroupDefinitionRecordRepository notificationGroupRepository,
        INotificationDefinitionRecordRepository notificationRepository,
        IDynamicNotificationDefinitionStoreInMemoryCache storeCache,
        IDistributedCache distributedCache,
        IOptions<AbpDistributedCacheOptions> cacheOptions,
        IOptions<NotificationDefinitionStoreOptions> storeOptions,
        IAbpDistributedLock distributedLock)
    {
        NotificationGroupRepository = notificationGroupRepository;
        NotificationRepository = notificationRepository;
        StoreCache = storeCache;
        DistributedCache = distributedCache;
        DistributedLock = distributedLock;
        StoreOptions = storeOptions.Value;
        CacheOptions = cacheOptions.Value;
    }

    public virtual async Task<NotificationDefinition?> GetOrNullAsync(string name)
    {
        if (!StoreOptions.IsDynamicNotificationStoreEnabled)
        {
            return null;
        }

        using (await StoreCache.SyncSemaphore.LockAsync())
        {
            await EnsureCacheIsUptoDateAsync();
            return StoreCache.GetNotificationOrNull(name);
        }
    }

    public virtual async Task<IReadOnlyList<NotificationDefinition>> GetNotificationsAsync()
    {
        if (!StoreOptions.IsDynamicNotificationStoreEnabled)
        {
            return Array.Empty<NotificationDefinition>();
        }

        using (await StoreCache.SyncSemaphore.LockAsync())
        {
            await EnsureCacheIsUptoDateAsync();
            return StoreCache.GetNotifications().ToImmutableList();
        }
    }

    public virtual async Task<IReadOnlyList<NotificationGroupDefinition>> GetGroupsAsync()
    {
        if (!StoreOptions.IsDynamicNotificationStoreEnabled)
        {
            return Array.Empty<NotificationGroupDefinition>();
        }

        using (await StoreCache.SyncSemaphore.LockAsync())
        {
            await EnsureCacheIsUptoDateAsync();
            return StoreCache.GetGroups().ToImmutableList();
        }
    }

    protected virtual async Task EnsureCacheIsUptoDateAsync()
    {
        if (StoreCache.LastCheckTime.HasValue &&
            DateTime.Now.Subtract(StoreCache.LastCheckTime.Value).TotalSeconds < 30)
        {
            /* We get the latest notification definitions with a small delay for optimization */
            return;
        }

        var stampInDistributedCache = await GetOrSetStampInDistributedCache();

        if (stampInDistributedCache == StoreCache.CacheStamp)
        {
            StoreCache.LastCheckTime = DateTime.Now;
            return;
        }

        await UpdateInMemoryStoreCache();

        StoreCache.CacheStamp = stampInDistributedCache;
        StoreCache.LastCheckTime = DateTime.Now;
    }

    protected virtual async Task UpdateInMemoryStoreCache()
    {
        var notificationGroupRecords = await NotificationGroupRepository.GetListAsync();
        var notificationRecords = await NotificationRepository.GetListAsync();

        await StoreCache.FillAsync(notificationGroupRecords, notificationRecords);
    }

    protected virtual async Task<string> GetOrSetStampInDistributedCache()
    {
        var cacheKey = GetCommonStampCacheKey();

        var stampInDistributedCache = await DistributedCache.GetStringAsync(cacheKey);
        if (stampInDistributedCache != null)
        {
            return stampInDistributedCache;
        }

        await using (var commonLockHandle = await DistributedLock
                         .TryAcquireAsync(GetCommonDistributedLockKey(), TimeSpan.FromMinutes(2)))
        {
            if (commonLockHandle == null)
            {
                /* This request will fail */
                throw new AbpException(
                    "Could not acquire distributed lock for notification definition common stamp check!");
            }

            stampInDistributedCache = await DistributedCache.GetStringAsync(cacheKey);
            if (stampInDistributedCache != null)
            {
                return stampInDistributedCache;
            }

            stampInDistributedCache = Guid.NewGuid().ToString();

            await DistributedCache.SetStringAsync(
                cacheKey,
                stampInDistributedCache,
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromDays(30)
                });
        }

        return stampInDistributedCache;
    }

    protected virtual string GetCommonStampCacheKey()
    {
        return $"{CacheOptions.KeyPrefix}_DigniteInMemoryNotificationCacheStamp";
    }

    protected virtual string GetCommonDistributedLockKey()
    {
        return $"{CacheOptions.KeyPrefix}_Common_DigniteNotificationUpdateLock";
    }
}
