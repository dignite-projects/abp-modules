using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Runs distribution off the request thread for large or subscription-resolved fan-outs.
/// </summary>
public class NotificationDistributionJob : AsyncBackgroundJob<NotificationDistributionJobArgs>, ITransientDependency
{
    protected INotificationDistributor Distributor { get; }

    protected ICurrentTenant CurrentTenant { get; }

    protected IUnitOfWorkManager UnitOfWorkManager { get; }

    protected IHostApplicationLifetime? HostApplicationLifetime { get; }

    public NotificationDistributionJob(
        INotificationDistributor distributor,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager)
        : this(distributor, currentTenant, unitOfWorkManager, null)
    {
    }

    public NotificationDistributionJob(
        INotificationDistributor distributor,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IHostApplicationLifetime? hostApplicationLifetime)
    {
        Distributor = distributor;
        CurrentTenant = currentTenant;
        UnitOfWorkManager = unitOfWorkManager;
        HostApplicationLifetime = hostApplicationLifetime;
    }

    public override Task ExecuteAsync(NotificationDistributionJobArgs args)
    {
        return ExecuteWithCancellationAsync(
            args,
            HostApplicationLifetime?.ApplicationStopping ?? CancellationToken.None);
    }

    /// <summary>
    /// Executes with explicit cancellation. ABP's background-job contract has no token parameter, so the normal
    /// override supplies the host shutdown token; direct/custom job runners can pass their own token.
    /// Deliberately NOT named <c>ExecuteAsync</c>: ABP's <see cref="BackgroundJobExecuter"/> locates the job's
    /// execute method by name via reflection, and a second public overload sharing that name throws
    /// <see cref="System.Reflection.AmbiguousMatchException"/> at job-execution time — every background-dispatched
    /// distribution (anything without explicit userIds, e.g. subscription-driven publish) would silently fail.
    /// </summary>
    public virtual async Task ExecuteWithCancellationAsync(
        NotificationDistributionJobArgs args,
        CancellationToken cancellationToken)
    {
        // Unlike an AppService call, ABP's background-job worker does not open a Unit Of Work around a job's
        // execution — Store.GetSubscriptionUserIdsAsync/InsertNotificationAsync/InsertUserNotificationsAsync all
        // resolve a repository backed by a DbContext with no ambient UoW to keep it alive, so the query throws
        // ObjectDisposedException the moment it actually runs against a real store (NullNotificationStore has no
        // DbContext, so this was invisible until NotificationCenter is installed). Open one explicitly here so the
        // notification insert, inbox rows, and the outbox event write for every recipient commit atomically.
        using (var uow = UnitOfWorkManager.Begin(requiresNew: true))
        // Restore the tenant before invoking even a custom distributor. The default distributor independently
        // scopes its whole operation to Notification.TenantId so direct calls cannot mix tenant/host data either.
        // ABP's BackgroundJobExecuter wraps a job in
        // CurrentTenant.Change(GetJobArgsTenantId(args)), which resolves to args.TenantId only for IMultiTenant
        // args — NotificationDistributionJobArgs is not — so the worker thread starts with no ambient tenant.
        // Everything downstream (INotificationStore, INotificationDefinitionManager, INotificationPermissionChecker)
        // reads the ambient tenant, and it flows into their fresh DI scopes because ABP's ICurrentTenantAccessor is
        // an AsyncLocal singleton. Notifiers are NOT downstream of this scope: they handle NotificationDeliveryRequestedEto,
        // and ABP's event bus re-enters the tenant from the event itself before invoking them.
        using (CurrentTenant.Change(args.Notification.TenantId, null))
        {
            await Distributor.DistributeAsync(
                args.Notification,
                args.UserIds,
                args.ExcludedUserIds,
                cancellationToken);

            await uow.CompleteAsync(cancellationToken);
        }
    }
}
