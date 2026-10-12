using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Resolves, persists, and publishes delivery work for a notification.
/// </summary>
public interface INotificationDistributor
{
    /// <summary>
    /// Distributes a notification to subscribers or to an explicit set of users.
    /// </summary>
    /// <param name="notification">
    /// The notification to distribute. Its <see cref="NotificationInfo.TenantId"/> is authoritative for the complete
    /// operation; <see langword="null"/> means host and never falls back to the ambient tenant.
    /// </param>
    /// <param name="userIds">
    /// The recipients. <see langword="null"/> resolves recipients from subscriptions; an empty array is an
    /// intentional no-op; a non-empty array targets those users explicitly. Duplicate IDs are removed before
    /// persistence and delivery publication. Definition feature and permission requirements are enforced for
    /// both explicit and subscription-derived candidates.
    /// </param>
    /// <param name="excludedUserIds">Optional user IDs to remove from the resolved recipient set.</param>
    /// <param name="cancellationToken">Cancellation observed between bounded recipient batches.</param>
    Task DistributeAsync(
        NotificationInfo notification,
        Guid[]? userIds = null,
        Guid[]? excludedUserIds = null,
        CancellationToken cancellationToken = default);
}
