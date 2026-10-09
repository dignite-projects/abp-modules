using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Json.SystemTextJson.Modifiers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Saves this process's static definitions to the shared store, step for step as ABP's <c>StaticPermissionSaver</c>:
/// <list type="number">
/// <item>Take this application's lock without waiting; another instance of the same application is already saving.</item>
/// <item>Compare an MD5 hash of the records (and the deleted lists) with the one cached for this application; when it
/// matches, nothing changed since the last save.</item>
/// <item>Take the lock common to every application (five minutes), so two applications never write at once.</item>
/// <item>In a unit of work of its own, insert new records, patch changed ones, and delete only the names listed in
/// <see cref="NotificationDefinitionStoreOptions.DeletedNotifications"/> /
/// <see cref="NotificationDefinitionStoreOptions.DeletedNotificationGroups"/> — never a record this process merely does
/// not define, since other applications write to the same tables.</item>
/// <item>When anything was written, renew the common stamp, which tells every dynamic store to reload; then cache the
/// new hash.</item>
/// </list>
/// </summary>
/// <remarks>
/// Cache keys use ABP's distributed cache key prefix and <see cref="IApplicationInfoAccessor.ApplicationName"/>: the
/// hash and the application lock are per application, the stamp and the common lock are shared — so every service
/// that writes to the same tables must use the same prefix and a distinct application name.
/// </remarks>
public class StaticNotificationDefinitionSaver : IStaticNotificationDefinitionSaver, ITransientDependency
{
    protected IStaticNotificationDefinitionStore StaticStore { get; }

    protected INotificationGroupDefinitionRecordRepository NotificationGroupRepository { get; }

    protected INotificationDefinitionRecordRepository NotificationRepository { get; }

    protected INotificationDefinitionSerializer NotificationSerializer { get; }

    protected IDistributedCache Cache { get; }

    protected IApplicationInfoAccessor ApplicationInfoAccessor { get; }

    protected IAbpDistributedLock DistributedLock { get; }

    protected NotificationDefinitionStoreOptions StoreOptions { get; }

    protected ICancellationTokenProvider CancellationTokenProvider { get; }

    protected AbpDistributedCacheOptions CacheOptions { get; }

    protected IUnitOfWorkManager UnitOfWorkManager { get; }

    public StaticNotificationDefinitionSaver(
        IStaticNotificationDefinitionStore staticStore,
        INotificationGroupDefinitionRecordRepository notificationGroupRepository,
        INotificationDefinitionRecordRepository notificationRepository,
        INotificationDefinitionSerializer notificationSerializer,
        IDistributedCache cache,
        IOptions<AbpDistributedCacheOptions> cacheOptions,
        IApplicationInfoAccessor applicationInfoAccessor,
        IAbpDistributedLock distributedLock,
        IOptions<NotificationDefinitionStoreOptions> storeOptions,
        ICancellationTokenProvider cancellationTokenProvider,
        IUnitOfWorkManager unitOfWorkManager)
    {
        StaticStore = staticStore;
        NotificationGroupRepository = notificationGroupRepository;
        NotificationRepository = notificationRepository;
        NotificationSerializer = notificationSerializer;
        Cache = cache;
        ApplicationInfoAccessor = applicationInfoAccessor;
        DistributedLock = distributedLock;
        CancellationTokenProvider = cancellationTokenProvider;
        UnitOfWorkManager = unitOfWorkManager;
        StoreOptions = storeOptions.Value;
        CacheOptions = cacheOptions.Value;
    }

    public virtual async Task SaveAsync()
    {
        await using var applicationLockHandle = await DistributedLock.TryAcquireAsync(GetApplicationDistributedLockKey());

        if (applicationLockHandle == null)
        {
            /* Another application instance is already doing it */
            return;
        }

        var cacheKey = GetApplicationHashCacheKey();
        var cachedHash = await Cache.GetStringAsync(cacheKey, CancellationTokenProvider.Token);

        var (notificationGroupRecords, notificationRecords) =
            await NotificationSerializer.SerializeAsync(await StaticStore.GetGroupsAsync());

        var currentHash = CalculateHash(
            notificationGroupRecords,
            notificationRecords,
            StoreOptions.DeletedNotificationGroups,
            StoreOptions.DeletedNotifications);

        if (cachedHash == currentHash)
        {
            return;
        }

        await using (var commonLockHandle = await DistributedLock.TryAcquireAsync(
                         GetCommonDistributedLockKey(),
                         TimeSpan.FromMinutes(5)))
        {
            if (commonLockHandle == null)
            {
                /* It will re-try */
                throw new AbpException("Could not acquire distributed lock for saving static notification definitions!");
            }

            using (var unitOfWork = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                try
                {
                    var hasChangesInGroups = await UpdateChangedNotificationGroupsAsync(notificationGroupRecords);
                    var hasChangesInNotifications = await UpdateChangedNotificationsAsync(notificationRecords);

                    if (hasChangesInGroups || hasChangesInNotifications)
                    {
                        await Cache.SetStringAsync(
                            GetCommonStampCacheKey(),
                            Guid.NewGuid().ToString(),
                            new DistributedCacheEntryOptions
                            {
                                SlidingExpiration = TimeSpan.FromDays(30)
                            },
                            CancellationTokenProvider.Token);
                    }
                }
                catch
                {
                    try
                    {
                        await unitOfWork.RollbackAsync();
                    }
                    catch
                    {
                        /* ignored */
                    }

                    throw;
                }

                await unitOfWork.CompleteAsync();
            }
        }

        await Cache.SetStringAsync(
            cacheKey,
            currentHash,
            new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromDays(30)
            },
            CancellationTokenProvider.Token);
    }

    protected virtual async Task<bool> UpdateChangedNotificationGroupsAsync(
        IEnumerable<NotificationGroupDefinitionRecord> notificationGroupRecords)
    {
        var newRecords = new List<NotificationGroupDefinitionRecord>();
        var changedRecords = new List<NotificationGroupDefinitionRecord>();

        var groupRecordsInDatabase = (await NotificationGroupRepository.GetListAsync())
            .ToDictionary(x => x.Name, StringComparer.Ordinal);

        foreach (var record in notificationGroupRecords)
        {
            var recordInDatabase = groupRecordsInDatabase.GetOrDefault(record.Name);

            if (recordInDatabase == null)
            {
                /* New group */
                newRecords.Add(record);
                continue;
            }

            if (record.HasSameData(recordInDatabase))
            {
                /* Not changed */
                continue;
            }

            /* Changed */
            recordInDatabase.Patch(record);
            changedRecords.Add(recordInDatabase);
        }

        /* Deleted: only what this application lists, never what it merely does not define */
        var deletedRecords = StoreOptions.DeletedNotificationGroups.Any()
            ? groupRecordsInDatabase.Values
                .Where(x => StoreOptions.DeletedNotificationGroups.Contains(x.Name))
                .ToArray()
            : Array.Empty<NotificationGroupDefinitionRecord>();

        if (newRecords.Any())
        {
            await NotificationGroupRepository.InsertManyAsync(newRecords);
        }

        if (changedRecords.Any())
        {
            await NotificationGroupRepository.UpdateManyAsync(changedRecords);
        }

        if (deletedRecords.Any())
        {
            await NotificationGroupRepository.DeleteManyAsync(deletedRecords);
        }

        return newRecords.Any() || changedRecords.Any() || deletedRecords.Any();
    }

    protected virtual async Task<bool> UpdateChangedNotificationsAsync(
        IEnumerable<NotificationDefinitionRecord> notificationRecords)
    {
        var newRecords = new List<NotificationDefinitionRecord>();
        var changedRecords = new List<NotificationDefinitionRecord>();

        var notificationRecordsInDatabase = (await NotificationRepository.GetListAsync())
            .ToDictionary(x => x.Name, StringComparer.Ordinal);

        foreach (var record in notificationRecords)
        {
            var recordInDatabase = notificationRecordsInDatabase.GetOrDefault(record.Name);

            if (recordInDatabase == null)
            {
                /* New definition */
                newRecords.Add(record);
                continue;
            }

            if (record.HasSameData(recordInDatabase))
            {
                /* Not changed */
                continue;
            }

            /* Changed */
            recordInDatabase.Patch(record);
            changedRecords.Add(recordInDatabase);
        }

        /* Deleted: only what this application lists, never what it merely does not define */
        var deletedRecords = new List<NotificationDefinitionRecord>();

        if (StoreOptions.DeletedNotifications.Any())
        {
            deletedRecords.AddRange(
                notificationRecordsInDatabase.Values.Where(x => StoreOptions.DeletedNotifications.Contains(x.Name)));
        }

        if (StoreOptions.DeletedNotificationGroups.Any())
        {
            deletedRecords.AddIfNotContains(
                notificationRecordsInDatabase.Values.Where(x => StoreOptions.DeletedNotificationGroups.Contains(x.GroupName)));
        }

        if (newRecords.Any())
        {
            await NotificationRepository.InsertManyAsync(newRecords);
        }

        if (changedRecords.Any())
        {
            await NotificationRepository.UpdateManyAsync(changedRecords);
        }

        if (deletedRecords.Any())
        {
            await NotificationRepository.DeleteManyAsync(deletedRecords);
        }

        return newRecords.Any() || changedRecords.Any() || deletedRecords.Any();
    }

    protected virtual string GetApplicationDistributedLockKey()
    {
        return $"{CacheOptions.KeyPrefix}_{ApplicationInfoAccessor.ApplicationName}_DigniteNotificationUpdateLock";
    }

    protected virtual string GetCommonDistributedLockKey()
    {
        return $"{CacheOptions.KeyPrefix}_Common_DigniteNotificationUpdateLock";
    }

    protected virtual string GetApplicationHashCacheKey()
    {
        return $"{CacheOptions.KeyPrefix}_{ApplicationInfoAccessor.ApplicationName}_DigniteNotificationsHash";
    }

    protected virtual string GetCommonStampCacheKey()
    {
        return $"{CacheOptions.KeyPrefix}_DigniteInMemoryNotificationCacheStamp";
    }

    protected virtual string CalculateHash(
        NotificationGroupDefinitionRecord[] notificationGroupRecords,
        NotificationDefinitionRecord[] notificationRecords,
        IEnumerable<string> deletedNotificationGroups,
        IEnumerable<string> deletedNotifications)
    {
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    new AbpIgnorePropertiesModifiers<NotificationGroupDefinitionRecord, Guid>().CreateModifyAction(x => x.Id),
                    new AbpIgnorePropertiesModifiers<NotificationDefinitionRecord, Guid>().CreateModifyAction(x => x.Id)
                }
            }
        };

        var stringBuilder = new StringBuilder();

        stringBuilder.Append("NotificationGroupRecords:");
        stringBuilder.AppendLine(JsonSerializer.Serialize(notificationGroupRecords, jsonSerializerOptions));

        stringBuilder.Append("NotificationRecords:");
        stringBuilder.AppendLine(JsonSerializer.Serialize(notificationRecords, jsonSerializerOptions));

        stringBuilder.Append("DeletedNotificationGroups:");
        stringBuilder.AppendLine(deletedNotificationGroups.JoinAsString(","));

        stringBuilder.Append("DeletedNotifications:");
        stringBuilder.Append(deletedNotifications.JoinAsString(","));

        return stringBuilder
            .ToString()
            .ToMd5();
    }
}
