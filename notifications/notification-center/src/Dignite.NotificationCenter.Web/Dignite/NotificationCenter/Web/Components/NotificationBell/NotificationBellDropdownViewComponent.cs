using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dignite.NotificationCenter.Web.Components.NotificationBell;

public class NotificationBellDropdownViewComponent : NotificationBellViewComponent
{
    public NotificationBellDropdownViewComponent(
        IUserNotificationAppService userNotificationAppService,
        INotificationDataTypeRegistry notificationDataTypeRegistry,
        IOptions<NotificationCenterWebOptions> webOptions)
        : base(userNotificationAppService, notificationDataTypeRegistry, webOptions)
    {
    }

    public override async Task<IViewComponentResult> InvokeAsync()
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Content(string.Empty);
        }

        var model = await CreateViewModelAsync();

        return View(
            "~/Dignite/NotificationCenter/Web/Components/NotificationBell/_DropdownItems.cshtml",
            model);
    }
}
