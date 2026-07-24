using Microsoft.AspNetCore.Mvc;

namespace Dignite.NotificationCenter.Web.Components.UnsupportedNotificationData;

public class UnsupportedNotificationDataViewComponent : ViewComponent
{
    public virtual IViewComponentResult Invoke(
        Dignite.Abp.Notifications.UnsupportedNotificationData data)
    {
        return View(
            "~/Dignite/NotificationCenter/Web/Components/UnsupportedNotificationData/Default.cshtml",
            data);
    }
}
