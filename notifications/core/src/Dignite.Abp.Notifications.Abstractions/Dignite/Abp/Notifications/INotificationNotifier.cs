using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The canonical notification delivery channel contract (SignalR, Email, Web Push, ...).
/// </summary>
public interface INotificationNotifier
{
    /// <summary>Stable channel name used for routing (e.g. "SignalR", "Email").</summary>
    string Name { get; }

    /// <summary>
    /// Delivers one recipient/channel request. Delivery is best-effort: a failure throws and is logged by the
    /// caller, not retried; a notifier that intentionally skips a recipient simply returns.
    /// </summary>
    Task DeliverAsync(
        NotificationDeliveryRequestedEto request,
        CancellationToken cancellationToken = default);
}
