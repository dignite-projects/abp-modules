using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace Dignite.Abp.Notifications;

public class DefaultNotificationPublisher : INotificationPublisher, ITransientDependency
{
    protected NotificationDistributionOptions Options { get; }

    protected INotificationDistributor Distributor { get; }

    protected IBackgroundJobManager BackgroundJobManager { get; }

    protected IGuidGenerator GuidGenerator { get; }

    protected IClock Clock { get; }

    protected ICurrentTenant CurrentTenant { get; }

    protected INotificationDefinitionManager DefinitionManager { get; }

    public DefaultNotificationPublisher(
        IOptions<NotificationDistributionOptions> options,
        INotificationDistributor distributor,
        IBackgroundJobManager backgroundJobManager,
        IGuidGenerator guidGenerator,
        IClock clock,
        ICurrentTenant currentTenant,
        INotificationDefinitionManager definitionManager)
    {
        Options = options.Value;
        Distributor = distributor;
        BackgroundJobManager = backgroundJobManager;
        GuidGenerator = guidGenerator;
        Clock = clock;
        CurrentTenant = currentTenant;
        DefinitionManager = definitionManager;
    }

    public virtual async Task PublishAsync(
        string notificationName,
        NotificationData? data = null,
        NotificationEntityIdentifier? entityIdentifier = null,
        NotificationSeverity severity = NotificationSeverity.Info,
        Guid[]? userIds = null,
        Guid[]? excludedUserIds = null)
    {
        if (userIds is { Length: 0 })
        {
            return;
        }

        // Fail fast on undefined notification names while the caller is still on the line, instead of
        // inside a background job.
        DefinitionManager.Get(notificationName);

        var notification = new NotificationInfo
        {
            Id = GuidGenerator.Create(),
            NotificationName = notificationName,
            Data = data,
            EntityTypeName = entityIdentifier?.EntityTypeName,
            EntityId = entityIdentifier?.EntityId,
            Severity = severity,
            CreationTime = Clock.Now,
            TenantId = CurrentTenant.Id
        };

        if (userIds != null && userIds.Distinct().Count() <= Options.DirectDistributionUserThreshold)
        {
            await Distributor.DistributeAsync(notification, userIds, excludedUserIds);
            return;
        }

        // Subscription resolution and large explicit fan-outs run off the request thread; the distributor
        // batches recipients internally.
        await BackgroundJobManager.EnqueueAsync(
            new NotificationDistributionJobArgs(notification, userIds, excludedUserIds));
    }
}
