using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dignite.NotificationCenter.Web.Components.NotificationBell;

public class NotificationBellViewComponent : ViewComponent
{
    protected IUserNotificationAppService UserNotificationAppService { get; }

    protected INotificationDataTypeRegistry NotificationDataTypeRegistry { get; }

    protected NotificationCenterWebOptions WebOptions { get; }

    public NotificationBellViewComponent(
        IUserNotificationAppService userNotificationAppService,
        INotificationDataTypeRegistry notificationDataTypeRegistry,
        IOptions<NotificationCenterWebOptions> webOptions)
    {
        UserNotificationAppService = userNotificationAppService;
        NotificationDataTypeRegistry = notificationDataTypeRegistry;
        WebOptions = webOptions.Value;
    }

    public virtual async Task<IViewComponentResult> InvokeAsync()
    {
        // The bell sits in the theme toolbar, which also renders on anonymous pages (e.g. the login page).
        // Querying per-user state without an authenticated user would throw — render nothing instead.
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Content(string.Empty);
        }

        var model = await CreateViewModelAsync();

        return View(
            "~/Dignite/NotificationCenter/Web/Components/NotificationBell/Default.cshtml",
            model);
    }

    protected virtual async Task<NotificationBellViewModel> CreateViewModelAsync()
    {
        var unreadCount = await UserNotificationAppService.GetUnreadCountAsync();
        var recent = await UserNotificationAppService.GetListAsync(new GetUserNotificationListInput
        {
            State = UserNotificationState.Unread,
            MaxResultCount = 10
        });

        var items = recent.Items.Select(CreateItemViewModel).ToList();

        return new NotificationBellViewModel(unreadCount, items, WebOptions.SignalRHubUrl);
    }

    protected virtual NotificationBellItemViewModel CreateItemViewModel(UserNotificationDto notification)
    {
        var discriminator = notification.Data == null
            ? null
            : NotificationDataTypeRegistry.GetDiscriminatorOrNull(notification.Data.GetType());

        var url = ResolveEntityUrl(notification);

        return new NotificationBellItemViewModel(notification, discriminator, url);
    }

    protected virtual string? ResolveEntityUrl(UserNotificationDto notification)
    {
        if (notification.EntityTypeName == null || notification.EntityId == null)
        {
            return null;
        }

        return WebOptions.EntityLinkResolvers.TryGetValue(notification.EntityTypeName, out var resolve)
            ? resolve(notification.EntityId)
            : null;
    }
}
