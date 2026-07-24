using Microsoft.AspNetCore.Mvc;

namespace Dignite.NotificationCenter.Web.Components.MessageNotificationData;

// NOTE: this namespace's leaf segment intentionally matches Dignite.Abp.Notifications.MessageNotificationData's
// simple name (folder = ViewComponent's logical name, per MVC convention) - the parameter type below must stay
// fully qualified, otherwise the compiler resolves the bare name to this namespace itself (CS0118), not the type.
public class MessageNotificationDataViewComponent : ViewComponent
{
    public virtual IViewComponentResult Invoke(Dignite.Abp.Notifications.MessageNotificationData data)
    {
        return View("~/Dignite/NotificationCenter/Web/Components/MessageNotificationData/Default.cshtml", data);
    }
}
