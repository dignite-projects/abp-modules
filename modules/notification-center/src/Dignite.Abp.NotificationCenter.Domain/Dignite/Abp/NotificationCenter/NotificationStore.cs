﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;

namespace Dignite.Abp.NotificationCenter;

public class NotificationStore : INotificationStore, ITransientDependency
{
    private readonly INotificationSubscriptionRepository _notificationSubscriptionRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserNotificationRepository _userNotificationRepository;
    private readonly IGuidGenerator _guidGenerator;

    public NotificationStore(
        IGuidGenerator guidGenerator,
        INotificationSubscriptionRepository notificationSubscriptionRepository,
        INotificationRepository notificationRepository,
        IUserNotificationRepository userNotificationRepository)
    {
        _guidGenerator = guidGenerator;
        _notificationSubscriptionRepository = notificationSubscriptionRepository;
        _notificationRepository = notificationRepository;
        _userNotificationRepository = userNotificationRepository;
    }

    /// <summary>
    /// Inserts a notification subscription.
    /// </summary>
    public async Task InsertSubscriptionAsync(NotificationSubscriptionInfo subscription)
    {
        if (!await IsSubscribedAsync(subscription.UserId, subscription.NotificationName, subscription.EntityTypeName, subscription.EntityId))
        {
            var notificationSubscription = new NotificationSubscription(_guidGenerator.Create(),subscription);
            await _notificationSubscriptionRepository.InsertAsync(notificationSubscription);
        }
    }

    /// <summary>
    /// Deletes a notification subscription.
    /// </summary>
    public async Task DeleteSubscriptionAsync(Guid userId, string notificationName, string entityTypeName, string entityId)
    {
        if (await IsSubscribedAsync(userId, notificationName, entityTypeName, entityId))
        {
            var notificationSubscription = await _notificationSubscriptionRepository.FindAsync(userId, notificationName, entityTypeName, entityId);
            await _notificationSubscriptionRepository.DeleteAsync(notificationSubscription);
        }
    }

    /// <summary>
    /// Inserts a notification.
    /// </summary>
    public async Task InsertNotificationAsync(NotificationInfo notificationInfo)
    {
        await _notificationRepository.InsertAsync(new Notification(notificationInfo));
    }

    /// <summary>
    /// Inserts a user notification.
    /// </summary>
    public async Task InsertUserNotificationAsync(UserNotificationInfo userNotification)
    {
        await _userNotificationRepository.InsertAsync(new UserNotification(_guidGenerator.Create(), userNotification));
    }

    /// <summary>
    /// Gets subscriptions for a notification.
    /// </summary>
    public async Task<List<NotificationSubscriptionInfo>> GetSubscriptionsAsync(string notificationName, string entityTypeName, string entityId)
    {
        var result = await _notificationSubscriptionRepository.GetListAsync(notificationName, entityTypeName, entityId);
        return result.Select(ns => new NotificationSubscriptionInfo(
            ns.UserId,
            ns.NotificationName,
            ns.EntityTypeName,
            ns.EntityId,
            ns.CreationTime,
            ns.TenantId
            )).ToList();
    }

    /// <summary>
    /// Gets subscriptions for a user.
    /// </summary>
    public async Task<List<NotificationSubscriptionInfo>> GetSubscriptionsAsync(Guid userId)
    {
        var result = await _notificationSubscriptionRepository.GetListAsync(userId);
        return result.Select(ns => new NotificationSubscriptionInfo(
            ns.UserId,
            ns.NotificationName,
            ns.EntityTypeName,
            ns.EntityId,
            ns.CreationTime,
            ns.TenantId
            )).ToList();
    }

    /// <summary>
    /// Checks if a user subscribed for a notification
    /// </summary>
    public async Task<bool> IsSubscribedAsync(Guid userId, string notificationName, string entityTypeName, string entityId)
    {
        var result = await _notificationSubscriptionRepository.FindAsync(userId, notificationName, entityTypeName, entityId);
        return result == null ? false : true;
    }

    /// <summary>
    /// Updates a user notification state.
    /// </summary>
    public async Task UpdateUserNotificationStateAsync(Guid userId, Guid notificationId, UserNotificationState state)
    {
        var result = await _userNotificationRepository.FindAsync(userId, notificationId);
        if (result != null)
        {
            result.State = state;
            await _userNotificationRepository.UpdateAsync(result);
        }
    }

    /// <summary>
    /// Updates all notification states for a user.
    /// </summary>
    public async Task UpdateAllUserNotificationStatesAsync(Guid userId, UserNotificationState state)
    {
        var result = await _userNotificationRepository.GetListAsync(userId,
            state == UserNotificationState.Read ?
                UserNotificationState.Unread :
                UserNotificationState.Read
            );
        foreach (var un in result)
        {
            un.State = state;
        }
        await _userNotificationRepository.UpdateManyAsync(result);
    }

    /// <summary>
    /// Deletes a user notification.
    /// </summary>
    public async Task DeleteUserNotificationAsync(Guid userId, Guid notificationId)
    {
        var result = await _userNotificationRepository.FindAsync(userId, notificationId);
        if (result != null)
        {
            await _userNotificationRepository.DeleteAsync(result);
        }
        if (!await _userNotificationRepository.AnyAsync(notificationId, userId))
        {
            await _notificationRepository.DeleteAsync(notificationId);
        }
    }

    /// <summary>
    /// Deletes all notifications of a user.
    /// </summary>
    public async Task DeleteAllUserNotificationsAsync(Guid userId, UserNotificationState? state = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var result = await _userNotificationRepository.GetListAsync(userId, state, 0, int.MaxValue, startDate, endDate);
        foreach (var un in result)
        {
            await _userNotificationRepository.DeleteAsync(un);
            if (!await _userNotificationRepository.AnyAsync(un.NotificationId, userId))
            {
                await _notificationRepository.DeleteAsync(un.NotificationId);
            }
        }
    }

    /// <summary>
    /// Gets notifications of a user.
    /// </summary>
    /// <param name="userId">User.</param>
    /// <param name="state">State</param>
    /// <param name="skipCount">Skip count.</param>
    /// <param name="maxResultCount">Maximum result count.</param>
    /// <param name="startDate">List notifications published after startDateTime</param>
    /// <param name="endDate">List notifications published before startDateTime</param>
    public async Task<List<UserNotificationWithNotification>> GetUserNotificationsAsync(Guid userId, UserNotificationState? state = null, int skipCount = 0, int maxResultCount = int.MaxValue, DateTime? startDate = null, DateTime? endDate = null)
    {
        var userNotifications = await _userNotificationRepository.GetListAsync(userId, state, skipCount, maxResultCount, startDate, endDate);
        var notifications = await _notificationRepository.GetListAsync(userNotifications.Select(un => un.NotificationId));
        var userNotificationWithNotifications=new List<UserNotificationWithNotification>();
        foreach (var un in userNotifications)
        {
            var notification = notifications.Single(n=>n.Id==un.NotificationId);
            userNotificationWithNotifications.Add(
                new UserNotificationWithNotification(
                    new UserNotificationInfo(
                        un.UserId,
                        un.NotificationId,
                        un.State,
                        un.TenantId),
                    new NotificationInfo(
                        notification.Id,
                        notification.NotificationName,
                        notification.Data.IsNullOrEmpty() ? null : JsonConvert.DeserializeObject(notification.Data, Type.GetType(notification.DataTypeName)) as NotificationData,
                        notification.EntityTypeName,
                        notification.EntityId,
                        notification.Severity,
                        notification.CreationTime,
                        notification.TenantId))
                );
        }

        return userNotificationWithNotifications;
    }

    /// <summary>
    /// Gets user notification count.
    /// </summary>
    /// <param name="user">User.</param>
    /// <param name="state">The state.</param>
    /// <param name="startDate">List notifications published after startDateTime</param>
    /// <param name="endDate">List notifications published before startDateTime</param>
    public async Task<int> GetUserNotificationCountAsync(Guid user, UserNotificationState? state = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        return await _userNotificationRepository.GetCountAsync(user, state, startDate, endDate);
    }
}