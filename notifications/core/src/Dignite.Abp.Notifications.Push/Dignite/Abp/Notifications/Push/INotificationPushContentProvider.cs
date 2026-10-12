using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications.Push;

/// <summary>
/// One link of the push content chain: builds the text for the notifications it recognizes and returns null for the
/// rest. Prefer deriving from <see cref="NotificationPushContentProvider{TData}"/>.
/// </summary>
public interface INotificationPushContentProvider
{
    /// <summary>Lower runs first. See <see cref="NotificationPushProviderOrders"/>.</summary>
    int Order { get; }

    Task<NotificationPushContent?> BuildOrNullAsync(
        NotificationPushBuildContext context,
        CancellationToken cancellationToken = default);
}
