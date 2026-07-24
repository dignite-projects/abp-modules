using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Volo.Abp;

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
        var localizer = data.ResourceName != null
            ? StringLocalizerFactory.CreateByResourceNameOrNull(data.ResourceName)
            : null;
        localizer ??= StringLocalizerFactory.CreateDefaultOrNull();

        var text = localizer == null
            ? data.Name
            : data.Arguments != null
                ? localizer[data.Name, data.Arguments.Values.ToArray()].Value
                : localizer[data.Name].Value;

        return View("~/Dignite/NotificationCenter/Web/Components/LocalizableMessageNotificationData/Default.cshtml", text);
    }
}
