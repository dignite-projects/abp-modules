using Dignite.Abp.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Dignite.NotificationCenter.Web.Components.LocalizableMessageNotificationData;

// NOTE: same fully-qualified-parameter workaround as MessageNotificationDataViewComponent - this
// namespace's leaf segment matches the NotificationData subclass's simple name.
public class LocalizableMessageNotificationDataViewComponent : ViewComponent
{
    protected IStringLocalizerFactory StringLocalizerFactory { get; }

    public LocalizableMessageNotificationDataViewComponent(IStringLocalizerFactory stringLocalizerFactory)
    {
        StringLocalizerFactory = stringLocalizerFactory;
    }

    public virtual IViewComponentResult Invoke(Dignite.Abp.Notifications.LocalizableMessageNotificationData data)
    {
        var text = data.Localize(StringLocalizerFactory);

        return View("~/Dignite/NotificationCenter/Web/Components/LocalizableMessageNotificationData/Default.cshtml", text);
    }
}
