using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Decides where a notification is distributed: an explicit fan-out of at most
/// <see cref="NotificationDistributionOptions.DirectDistributionUserThreshold"/> distinct users inline, through
/// <see cref="INotificationDistributor"/>; anything larger, and every subscription-resolved notification, through a
/// single <see cref="NotificationDistributionJob"/> on this process's job queue. Shared by the local publisher and by
/// the handler of notifications published in other processes, so both paths have the same bounds.
/// </summary>
public class NotificationDistributionDispatcher : ITransientDependency
{
    protected NotificationDistributionOptions Options { get; }

    protected INotificationDistributor Distributor { get; }

    protected IBackgroundJobManager BackgroundJobManager { get; }

    public NotificationDistributionDispatcher(
        IOptions<NotificationDistributionOptions> options,
        INotificationDistributor distributor,
        IBackgroundJobManager backgroundJobManager)
    {
        Options = options.Value;
        Distributor = distributor;
        BackgroundJobManager = backgroundJobManager;
    }

    /// <param name="notification">The notification, with its tenant and payload already set.</param>
    /// <param name="userIds">
    /// <see langword="null"/> resolves recipients from subscriptions; an empty array is a no-op; a non-empty array
    /// targets those users. Duplicates are removed before the threshold is evaluated.
    /// </param>
    /// <param name="excludedUserIds">Optional user IDs to remove from the resolved recipient set.</param>
    /// <param name="cancellationToken">Forwarded to an inline distribution; enqueuing a job is not cancellable.</param>
    public virtual async Task DispatchAsync(
        NotificationInfo notification,
        Guid[]? userIds,
        Guid[]? excludedUserIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds is { Length: 0 })
        {
            return;
        }

        if (ShouldDistributeInline(userIds))
        {
            await Distributor.DistributeAsync(notification, userIds, excludedUserIds, cancellationToken);
            return;
        }

        // Subscription resolution and large explicit fan-outs run off the request thread; the distributor
        // batches recipients internally.
        await BackgroundJobManager.EnqueueAsync(
            new NotificationDistributionJobArgs(notification, userIds, excludedUserIds));
    }

    protected virtual bool ShouldDistributeInline(Guid[]? userIds)
    {
        return userIds != null && userIds.Distinct().Count() <= Options.DirectDistributionUserThreshold;
    }
}
