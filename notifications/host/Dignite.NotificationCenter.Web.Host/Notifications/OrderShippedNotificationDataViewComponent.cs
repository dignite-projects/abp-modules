using Microsoft.AspNetCore.Mvc;

namespace Dignite.NotificationCenter.Web.Host.Notifications;

/// <summary>
/// Demo of the per-discriminator custom renderer slot: registered in the host module against the
/// "Demo.OrderShipped" discriminator (never a CLR type name) via NotificationCenterWebOptions.DataViewComponents.
/// </summary>
public class OrderShippedNotificationDataViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(OrderShippedNotificationData data)
    {
        return View(data);
    }
}
