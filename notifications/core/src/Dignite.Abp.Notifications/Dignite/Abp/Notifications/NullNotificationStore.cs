using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications;

/// <summary>
/// No-op store used in stateless forwarding mode (no NotificationCenter installed). Notifications are still
/// published as delivery events; nothing is persisted, and there are no subscriptions or inbox.
/// </summary>
public class NullNotificationStore : INotificationStore, ISingletonDependency
{
    public Task InsertSubscriptionAsync(
        NotificationSubscriptionInfo subscription,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteSubscriptionAsync(
        Guid userId,
        string notificationName,
        string? entityTypeName,
        string? entityId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<bool> IsSubscribedAsync(
        Guid userId,
        string notificationName,
        string? entityTypeName,
        string? entityId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<List<NotificationSubscriptionInfo>> GetSubscriptionsAsync(
        string notificationName,
        string? entityTypeName,
        string? entityId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<NotificationSubscriptionInfo>());
    }

    public Task<List<Guid>> GetSubscriptionUserIdsAsync(
        string notificationName,
        string? entityTypeName,
        string? entityId,
        Guid? afterUserId,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<Guid>());
    }

    public Task<List<NotificationSubscriptionInfo>> GetSubscriptionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<NotificationSubscriptionInfo>());
    }

    public Task InsertNotificationAsync(
        NotificationInfo notification,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task InsertUserNotificationAsync(
        UserNotificationInfo userNotification,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task InsertUserNotificationsAsync(
        IReadOnlyCollection<UserNotificationInfo> userNotifications,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task UpdateUserNotificationStateAsync(
        Guid userId,
        Guid notificationId,
        UserNotificationState state,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task UpdateAllUserNotificationStatesAsync(
        Guid userId,
        UserNotificationState state,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteUserNotificationAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAllUserNotificationsAsync(
        Guid userId,
        UserNotificationState? state = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<List<UserNotificationWithNotification>> GetUserNotificationsAsync(
        Guid userId,
        UserNotificationState? state = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<UserNotificationWithNotification>());
    }

    public Task<int> GetUserNotificationCountAsync(
        Guid userId,
        UserNotificationState? state = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }
}
