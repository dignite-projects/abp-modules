using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Persistence abstraction for notifications and subscriptions. The core depends only on this; the optional
/// NotificationCenter module supplies a real implementation, otherwise <see cref="NullNotificationStore"/> is used.
/// </summary>
public interface INotificationStore
{
    /// <summary>Inserts one definition-wide or entity-specific subscription identity.</summary>
    Task InsertSubscriptionAsync(
        NotificationSubscriptionInfo subscription,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes only the exact definition-wide or entity-specific subscription identity.</summary>
    Task DeleteSubscriptionAsync(
        Guid userId,
        string notificationName,
        string? entityTypeName,
        string? entityId,
        CancellationToken cancellationToken = default);

    /// <summary>Checks only the exact definition-wide or entity-specific subscription identity.</summary>
    Task<bool> IsSubscribedAsync(
        Guid userId,
        string notificationName,
        string? entityTypeName,
        string? entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recipients for a notification. A definition-wide notification matches definition-wide subscriptions;
    /// an entity notification matches the union of definition-wide subscriptions and subscriptions to that exact entity.
    /// </summary>
    Task<List<NotificationSubscriptionInfo>> GetSubscriptionsAsync(
        string notificationName,
        string? entityTypeName,
        string? entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next stable, ordered page of distinct subscription recipient IDs in the current tenant.
    /// <paramref name="afterUserId"/> is an exclusive keyset cursor; <see langword="null"/> starts the scan.
    /// </summary>
    Task<List<Guid>> GetSubscriptionUserIdsAsync(
        string notificationName,
        string? entityTypeName,
        string? entityId,
        Guid? afterUserId,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    /// <summary>Gets every distinct subscription identity stored for the user in the current tenant.</summary>
    Task<List<NotificationSubscriptionInfo>> GetSubscriptionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task InsertNotificationAsync(
        NotificationInfo notification,
        CancellationToken cancellationToken = default);

    Task InsertUserNotificationAsync(
        UserNotificationInfo userNotification,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts one already-bounded group of inbox rows.</summary>
    Task InsertUserNotificationsAsync(
        IReadOnlyCollection<UserNotificationInfo> userNotifications,
        CancellationToken cancellationToken = default);

    Task UpdateUserNotificationStateAsync(
        Guid userId,
        Guid notificationId,
        UserNotificationState state,
        CancellationToken cancellationToken = default);

    Task UpdateAllUserNotificationStatesAsync(
        Guid userId,
        UserNotificationState state,
        CancellationToken cancellationToken = default);

    Task DeleteUserNotificationAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task DeleteAllUserNotificationsAsync(
        Guid userId,
        UserNotificationState? state = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    Task<List<UserNotificationWithNotification>> GetUserNotificationsAsync(
        Guid userId,
        UserNotificationState? state = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    Task<int> GetUserNotificationCountAsync(
        Guid userId,
        UserNotificationState? state = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);
}
