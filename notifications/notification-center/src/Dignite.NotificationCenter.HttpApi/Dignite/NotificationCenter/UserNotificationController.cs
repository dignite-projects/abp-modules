using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.NotificationCenter;

/// <summary>
/// REST endpoints for the current user's notification inbox. A thin controller that delegates to
/// <see cref="IUserNotificationAppService"/> — the AppService owns all authorization (its class-level
/// [Authorize]) and per-user scoping. Exposed under <c>/api/notification-center/notifications</c>;
/// subscriptions live on <see cref="NotificationSubscriptionController"/>. Derives from
/// <see cref="NotificationCenterController"/> for the module's localization resource.
/// </summary>
[RemoteService(Name = "NotificationCenter")]
[Area("notification-center")]
[Route("api/notification-center/notifications")]
public class UserNotificationController : NotificationCenterController, IUserNotificationAppService
{
    protected IUserNotificationAppService UserNotificationAppService { get; }

    public UserNotificationController(IUserNotificationAppService userNotificationAppService)
    {
        UserNotificationAppService = userNotificationAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<UserNotificationDto>> GetListAsync(GetUserNotificationListInput input)
    {
        return UserNotificationAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("unread-count")]
    public virtual Task<int> GetUnreadCountAsync()
    {
        return UserNotificationAppService.GetUnreadCountAsync();
    }

    [HttpPost]
    [Route("{notificationId}/mark-as-read")]
    public virtual Task MarkAsReadAsync(Guid notificationId)
    {
        return UserNotificationAppService.MarkAsReadAsync(notificationId);
    }

    [HttpPost]
    [Route("mark-all-as-read")]
    public virtual Task MarkAllAsReadAsync()
    {
        return UserNotificationAppService.MarkAllAsReadAsync();
    }

    [HttpDelete]
    [Route("{notificationId}")]
    public virtual Task DeleteAsync(Guid notificationId)
    {
        return UserNotificationAppService.DeleteAsync(notificationId);
    }

    [HttpDelete]
    [Route("read")]
    public virtual Task DeleteAllReadAsync()
    {
        return UserNotificationAppService.DeleteAllReadAsync();
    }
}
