using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.NotificationCenter;

/// <summary>
/// Headless subscription API for the current user. A subscription's identity includes its optional entity
/// scope (see notifications-invariants.md §6), so subscribe/unsubscribe take the full
/// <see cref="NotificationSubscriptionScopeDto"/> — a definition-wide subscription leaves both entity fields null,
/// an entity-scoped one supplies both.
/// </summary>
public interface INotificationSubscriptionAppService : IApplicationService
{
    Task<ListResultDto<NotificationSubscriptionDto>> GetSubscriptionsAsync();

    /// <summary>Subscribes the current user to exactly the supplied definition-wide or entity scope.</summary>
    Task SubscribeAsync(NotificationSubscriptionScopeDto input);

    /// <summary>Removes exactly the supplied definition-wide or entity scope.</summary>
    Task UnsubscribeAsync(NotificationSubscriptionScopeDto input);
}
