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

        var filter = await ResolveGroupFilterAsync(input.GroupName);
        if (filter == null)
        {
            return new PagedResultDto<UserNotificationDto>(0, new List<UserNotificationDto>());
        }

        var totalCount = await Store.GetUserNotificationCountAsync(
            userId, input.State, input.StartDate, input.EndDate, filter.NotificationNames,
            filter.ExcludedNotificationNames);

        var items = await Store.GetUserNotificationsAsync(
            userId, input.State, input.SkipCount, input.MaxResultCount, input.StartDate, input.EndDate,
            filter.NotificationNames, filter.ExcludedNotificationNames);

        var dtos = new List<UserNotificationDto>(items.Count);
        foreach (var item in items)
        {
            dtos.Add(await MapToDtoAsync(item));
        }

        return new PagedResultDto<UserNotificationDto>(totalCount, dtos);
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

        // A group's definitions are taken from the merged definition list by GroupName rather than from the group
        // object: a definition from the definition store may belong to a group this process also defines.
        var definitions = await DefinitionManager.GetAllAsync();
        var definedNames = definitions.Select(definition => definition.Name).ToHashSet(StringComparer.Ordinal);

        var groups = new List<UserNotificationGroupDto>();
        foreach (var group in await DefinitionManager.GetGroupsAsync())
        {
            var unreadCount = definitions
                .Where(definition => definition.GroupName == group.Name)
                .Sum(definition => unreadByName.GetValueOrDefault(definition.Name));

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
    /// applies no filter. Returns <see langword="null"/> for an unknown group (or one without definitions), which
    /// can match nothing.
    /// </summary>
    protected virtual async Task<GroupFilter?> ResolveGroupFilterAsync(string? groupName)
    {
        if (groupName == null)
        {
            return new GroupFilter(null, null);
        }

        var definitions = await DefinitionManager.GetAllAsync();

        if (groupName == NotificationCenterConsts.OtherGroupName)
        {
            return new GroupFilter(null, definitions.Select(definition => definition.Name).ToList());
        }

        if (await DefinitionManager.GetGroupOrNullAsync(groupName) == null)
        {
            return null;
        }

        var notificationNames = definitions
            .Where(definition => definition.GroupName == groupName)
            .Select(definition => definition.Name)
            .ToList();

        return notificationNames.Count == 0 ? null : new GroupFilter(notificationNames, null);
    }

    protected virtual async Task<UserNotificationDto> MapToDtoAsync(UserNotificationWithNotification source)
    {
        // Display names are localized here, per the current reader's culture (fixes the reference implementation's
        // publish-time culture baking — roadmap problem F).
        var definition = await DefinitionManager.GetOrNullAsync(source.Notification.NotificationName);
        var group = definition == null ? null : await DefinitionManager.GetGroupOrNullAsync(definition.GroupName);

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

    /// <summary>The store name filters an inbox group selects.</summary>
    /// <param name="NotificationNames">Only these notification names; <see langword="null"/> for no inclusion filter.</param>
    /// <param name="ExcludedNotificationNames">Every name except these; <see langword="null"/> for no exclusion filter.</param>
    protected sealed record GroupFilter(
        IReadOnlyCollection<string>? NotificationNames,
        IReadOnlyCollection<string>? ExcludedNotificationNames);
}
