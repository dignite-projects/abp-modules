using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Persistence abstraction for notifications and subscriptions. The core depends only on this; the optional
/// NotificationCenter module supplies a real implementation, otherwise the no-op <c>NullNotificationStore</c> of
/// Dignite.Abp.Notifications.Distribution is used. It lives in Abstractions so that the store implementations never depend
/// on the distribution pipeline.
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

    /// <summary>
    /// Erases everything the store keeps about the user — every inbox row, whatever its state, and every subscription
    /// identity — in every tenant, for example when the user's personal data is erased. The shared notification
    /// payload is not touched. Rows are removed in bulk without being loaded, so this is not the way to clear one
    /// tenant's inbox: use <see cref="DeleteAllUserNotificationsAsync"/> for that.
    /// </summary>
    Task DeleteAllUserDataAsync(
        Guid userId,
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

    /// <summary>
    /// Gets a page of the user's inbox, newest first. <paramref name="notificationNames"/> keeps only rows of those
    /// definitions; <paramref name="excludedNotificationNames"/> drops rows of those definitions (used for rows whose
    /// definition no longer exists). Both match the inbox row's notification name using the database's string
    /// comparison, so definition names that differ only by case are not distinguished on case-insensitive collations.
    /// </summary>
    Task<List<UserNotificationWithNotification>> GetUserNotificationsAsync(
        Guid userId,
        UserNotificationState? state = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        DateTime? startDate = null,
        DateTime? endDate = null,
        IReadOnlyCollection<string>? notificationNames = null,
        IReadOnlyCollection<string>? excludedNotificationNames = null,
        CancellationToken cancellationToken = default);

    /// <summary>Counts the user's inbox rows using the same filters as <see cref="GetUserNotificationsAsync"/>.</summary>
    Task<int> GetUserNotificationCountAsync(
        Guid userId,
        UserNotificationState? state = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        IReadOnlyCollection<string>? notificationNames = null,
        IReadOnlyCollection<string>? excludedNotificationNames = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the user's unread inbox row count per notification name, in a single grouped query. Names with no unread
    /// rows are omitted.
    /// </summary>
    Task<Dictionary<string, int>> GetUnreadCountsByNotificationNameAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
