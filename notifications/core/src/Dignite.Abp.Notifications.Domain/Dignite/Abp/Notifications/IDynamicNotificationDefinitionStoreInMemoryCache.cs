using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The process-wide copy of the stored definitions, as ABP's <c>IDynamicPermissionDefinitionStoreInMemoryCache</c>:
/// the stamp it was filled for, when the stamp was last compared, and the semaphore that serializes both.
/// </summary>
public interface IDynamicNotificationDefinitionStoreInMemoryCache
{
    string? CacheStamp { get; set; }

    SemaphoreSlim SyncSemaphore { get; }

    DateTime? LastCheckTime { get; set; }

    Task FillAsync(
        List<NotificationGroupDefinitionRecord> notificationGroupRecords,
        List<NotificationDefinitionRecord> notificationRecords);

    NotificationDefinition? GetNotificationOrNull(string name);

    IReadOnlyList<NotificationDefinition> GetNotifications();

    IReadOnlyList<NotificationGroupDefinition> GetGroups();
}
