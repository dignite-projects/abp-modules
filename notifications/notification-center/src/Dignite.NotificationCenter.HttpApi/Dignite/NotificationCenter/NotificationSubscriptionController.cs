using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.NotificationCenter;

/// <summary>
/// REST endpoints for the current user's notification subscriptions. A thin controller that delegates to
/// <see cref="INotificationSubscriptionAppService"/> — the AppService owns all authorization (its class-level
/// [Authorize]) and per-user scoping. A subscription's identity includes its optional entity scope
/// (notifications-invariants.md §6), so subscribe/unsubscribe take the full
/// <see cref="NotificationSubscriptionScopeDto"/>: a definition-wide subscription leaves both entity fields
/// null, an entity-scoped one supplies both. Exposed under <c>/api/notification-center/subscriptions</c>.
/// Derives from <see cref="NotificationCenterController"/> for the module's localization resource.
/// </summary>
[RemoteService(Name = "NotificationCenter")]
[Area("notification-center")]
[Route("api/notification-center/subscriptions")]
public class NotificationSubscriptionController : NotificationCenterController, INotificationSubscriptionAppService
{
    protected INotificationSubscriptionAppService SubscriptionAppService { get; }

    public NotificationSubscriptionController(INotificationSubscriptionAppService subscriptionAppService)
    {
        SubscriptionAppService = subscriptionAppService;
    }

    [HttpGet]
    public virtual Task<ListResultDto<NotificationSubscriptionDto>> GetSubscriptionsAsync()
    {
        return SubscriptionAppService.GetSubscriptionsAsync();
    }

    [HttpPost]
    public virtual Task SubscribeAsync([FromBody] NotificationSubscriptionScopeDto input)
    {
        return SubscriptionAppService.SubscribeAsync(input);
    }

    [HttpDelete]
    public virtual Task UnsubscribeAsync([FromQuery] NotificationSubscriptionScopeDto input)
    {
        return SubscriptionAppService.UnsubscribeAsync(input);
    }
}
