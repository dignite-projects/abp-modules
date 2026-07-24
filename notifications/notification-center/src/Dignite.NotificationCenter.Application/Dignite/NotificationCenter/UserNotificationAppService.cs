using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Localization;
using Volo.Abp.Users;

namespace Dignite.NotificationCenter;

[Authorize]
public class UserNotificationAppService : ApplicationService, IUserNotificationAppService
{
    protected INotificationStore Store { get; }

    protected INotificationDefinitionManager DefinitionManager { get; }

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

        var totalCount = await Store.GetUserNotificationCountAsync(
            userId, input.State, input.StartDate, input.EndDate);

        var items = await Store.GetUserNotificationsAsync(
            userId, input.State, input.SkipCount, input.MaxResultCount, input.StartDate, input.EndDate);

        return new PagedResultDto<UserNotificationDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public virtual Task<int> GetUnreadCountAsync()
    {
        return Store.GetUserNotificationCountAsync(CurrentUser.GetId(), UserNotificationState.Unread);
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

    protected virtual UserNotificationDto MapToDto(UserNotificationWithNotification source)
    {
        // Display name is localized here, per the current reader's culture (fixes the reference implementation's
        // publish-time culture baking — roadmap problem F).
        var definition = DefinitionManager.GetOrNull(source.Notification.NotificationName);

        return new UserNotificationDto
        {
            Id = source.UserNotification.Id,
            UserId = source.UserNotification.UserId,
            NotificationId = source.Notification.Id,
            NotificationName = source.Notification.NotificationName,
            NotificationDisplayName = definition?.DisplayName.Localize(StringLocalizerFactory).Value,
            Data = source.Notification.Data,
            EntityTypeName = source.Notification.EntityTypeName,
            EntityId = source.Notification.EntityId,
            Severity = source.Notification.Severity,
            CreationTime = source.Notification.CreationTime,
            State = source.UserNotification.State
        };
    }
}
