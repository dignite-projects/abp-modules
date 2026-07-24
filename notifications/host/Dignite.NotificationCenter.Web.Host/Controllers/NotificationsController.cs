using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.NotificationCenter.Web.Host.Controllers;

/// <summary>
/// MVC-side counterpart to the Angular demo's notifications test page (angular/src/app/notifications):
/// exercises the same DemoNotificationAppService endpoints so the bell/inbox can be smoke-tested from the
/// ASP.NET Core host too, without needing the Angular app running.
/// </summary>
[Authorize]
public class NotificationsController : AbpController
{
    public virtual IActionResult Index()
    {
        return View();
    }
}
