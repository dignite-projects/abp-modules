using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Dignite.NotificationCenter.Web.Components.NotificationSubscriptions;

public class NotificationSubscriptionsViewComponent : ViewComponent
{
    protected INotificationSubscriptionAppService SubscriptionAppService { get; }

    public NotificationSubscriptionsViewComponent(INotificationSubscriptionAppService subscriptionAppService)
    {
        SubscriptionAppService = subscriptionAppService;
    }

    public virtual async Task<IViewComponentResult> InvokeAsync()
    {
        var result = await SubscriptionAppService.GetSubscriptionsAsync();
        return View(
            "~/Dignite/NotificationCenter/Web/Components/NotificationSubscriptions/Default.cshtml",
            result.Items.ToList());
    }
}
