using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Decides which external channels one notification is delivered on. Replaceable: a host that needs dynamic
/// routing (per tenant, per severity, from ABP Settings) supplies its own implementation; the default only reads
/// <see cref="NotificationRoutingOptions"/>. Per-recipient preferences do not belong here — the result applies to
/// every recipient of the notification.
/// </summary>
public interface INotificationChannelResolver
{
    /// <summary>
    /// Resolves the external channels for one notification. Called once per distribution, before recipients are
    /// batched. Null or empty = inbox-only. Runs inside <c>CurrentTenant.Change(notification.TenantId)</c>.
    /// </summary>
    Task<string[]?> ResolveAsync(
        NotificationDefinition definition,
        NotificationInfo notification,
        CancellationToken cancellationToken = default);
}
