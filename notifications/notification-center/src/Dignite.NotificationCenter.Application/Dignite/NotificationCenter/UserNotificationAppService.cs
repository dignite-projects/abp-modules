using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Localization;
using Volo.Abp.Users;

namespace Dignite.NotificationCenter;

[Authorize]
public class UserNotificationAppService : NotificationCenterAppService, IUserNotificationAppService
{
    protected INotificationStore Store { get; }

    protected INotificationDefinitionManager DefinitionManager { get; }

    /// <summary>
    /// Hydrates the stored payload JSON for the DTO. The read is tolerant: a payload this process cannot read (an
    /// unregistered discriminator, malformed JSON) becomes <see cref="UnsupportedNotificationData"/>, never an error.
    /// </summary>
    protected INotificationDataSerializer DataSerializer =>
        LazyServiceProvider.LazyGetRequiredService<INotificationDataSerializer>();

    public UserNotificationAppService(
        INotificationStore store,
        INotificationDefinitionManager definitionManager)
    {
        Store = store;
        DefinitionManager = definitionManager;
    }

    public virtual async Task<PagedResultDto<UserNotificationDto>> GetListAsync(GetUserNotificationListInput input)
    {
        var userId = CurrentUser.GetId();

        if (!TryResolveGroupFilter(input.GroupName, out var notificationNames, out var excludedNotificationNames))
        {
            return new PagedResultDto<UserNotificationDto>(0, new List<UserNotificationDto>());
        }

        var totalCount = await Store.GetUserNotificationCountAsync(
            userId, input.State, input.StartDate, input.EndDate, notificationNames, excludedNotificationNames);

        var items = await Store.GetUserNotificationsAsync(
            userId, input.State, input.SkipCount, input.MaxResultCount, input.StartDate, input.EndDate,
            notificationNames, excludedNotificationNames);

        return new PagedResultDto<UserNotificationDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public virtual Task<int> GetUnreadCountAsync()
    {
        return Store.GetUserNotificationCountAsync(CurrentUser.GetId(), UserNotificationState.Unread);
    }

    public virtual async Task<ListResultDto<UserNotificationGroupDto>> GetGroupsAsync()
    {
        var userId = CurrentUser.GetId();

        var unreadByName = await Store.GetUnreadCountsByNotificationNameAsync(userId);
        var availableGroupNames = (await DefinitionManager.GetAllAvailableAsync(userId))
            .Select(definition => definition.GroupName)
            .ToHashSet(StringComparer.Ordinal);

        var groups = new List<UserNotificationGroupDto>();
        var definedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in DefinitionManager.GetGroups())
        {
            var unreadCount = 0;
            foreach (var definition in group.Notifications)
            {
                definedNames.Add(definition.Name);
                unreadCount += unreadByName.GetValueOrDefault(definition.Name);
            }

            if (unreadCount > 0 || availableGroupNames.Contains(group.Name))
            {
                groups.Add(new UserNotificationGroupDto
                {
                    Name = group.Name,
                    DisplayName = GetGroupDisplayName(group),
                    UnreadCount = unreadCount
                });
            }
        }

        var otherUnreadCount = unreadByName
            .Where(pair => !definedNames.Contains(pair.Key))
            .Sum(pair => pair.Value);
        if (otherUnreadCount > 0
            || await Store.GetUserNotificationCountAsync(userId, excludedNotificationNames: definedNames) > 0)
        {
            groups.Add(new UserNotificationGroupDto
            {
                Name = NotificationCenterConsts.OtherGroupName,
                DisplayName = GetGroupDisplayName(null),
                UnreadCount = otherUnreadCount
            });
        }

        return new ListResultDto<UserNotificationGroupDto>(groups);
    }

    public virtual Task MarkAsReadAsync(Guid notificationId)
    {
        return Store.UpdateUserNotificationStateAsync(
            CurrentUser.GetId(), notificationId, UserNotificationState.Read);
    }

    public virtual Task MarkAllAsReadAsync()
    {
        return Store.UpdateAllUserNotificationStatesAsync(
            CurrentUser.GetId(), UserNotificationState.Read);
    }

    public virtual Task DeleteAsync(Guid notificationId)
    {
        return Store.DeleteUserNotificationAsync(CurrentUser.GetId(), notificationId);
    }

    public virtual Task DeleteAllReadAsync()
    {
        return Store.DeleteAllUserNotificationsAsync(CurrentUser.GetId(), UserNotificationState.Read);
    }

    /// <summary>
    /// Translates a group name into store name filters: a defined group keeps only its definitions, the synthetic
    /// <see cref="NotificationCenterConsts.OtherGroupName"/> excludes every defined name, and <see langword="null"/>
    /// applies no filter. Returns <see langword="false"/> for an unknown group (or one without definitions), which
    /// can match nothing.
    /// </summary>
    protected virtual bool TryResolveGroupFilter(
        string? groupName,
        out IReadOnlyCollection<string>? notificationNames,
        out IReadOnlyCollection<string>? excludedNotificationNames)
    {
        notificationNames = null;
        excludedNotificationNames = null;

        if (groupName == null)
        {
            return true;
        }

        if (groupName == NotificationCenterConsts.OtherGroupName)
        {
            excludedNotificationNames = DefinitionManager.GetAll().Select(definition => definition.Name).ToList();
            return true;
        }

        var group = DefinitionManager.GetGroupOrNull(groupName);
        if (group == null || group.Notifications.Count == 0)
        {
            return false;
        }

        notificationNames = group.Notifications.Select(definition => definition.Name).ToList();
        return true;
    }

    protected virtual UserNotificationDto MapToDto(UserNotificationWithNotification source)
    {
        // Display names are localized here, per the current reader's culture (fixes the reference implementation's
        // publish-time culture baking — roadmap problem F).
        var definition = DefinitionManager.GetOrNull(source.Notification.NotificationName);
        var group = definition == null ? null : DefinitionManager.GetGroupOrNull(definition.GroupName);

        return new UserNotificationDto
        {
            Id = source.UserNotification.Id,
            UserId = source.UserNotification.UserId,
            NotificationId = source.Notification.Id,
            NotificationName = source.Notification.NotificationName,
            NotificationDisplayName = definition?.DisplayName.Localize(StringLocalizerFactory).Value,
            GroupName = group?.Name ?? NotificationCenterConsts.OtherGroupName,
            GroupDisplayName = GetGroupDisplayName(group),
            Data = DataSerializer.Deserialize(source.Notification.DataJson),
            EntityTypeName = source.Notification.EntityTypeName,
            EntityId = source.Notification.EntityId,
            Severity = source.Notification.Severity,
            CreationTime = source.Notification.CreationTime,
            State = source.UserNotification.State
        };
    }
}
