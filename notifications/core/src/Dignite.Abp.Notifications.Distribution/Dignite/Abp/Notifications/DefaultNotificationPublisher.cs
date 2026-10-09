using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Publishes in the process that distributes: small explicit fan-outs are distributed inline, everything else through a
/// background job — the decision belongs to <see cref="NotificationDistributionDispatcher"/>.
/// </summary>
public class DefaultNotificationPublisher : INotificationPublisher, ITransientDependency
{
    protected NotificationDistributionDispatcher Dispatcher { get; }

    protected IGuidGenerator GuidGenerator { get; }

    protected IClock Clock { get; }

    protected ICurrentTenant CurrentTenant { get; }

    protected INotificationDefinitionManager DefinitionManager { get; }

    protected INotificationDataSerializer DataSerializer { get; }

    public DefaultNotificationPublisher(
        NotificationDistributionDispatcher dispatcher,
        IGuidGenerator guidGenerator,
        IClock clock,
        ICurrentTenant currentTenant,
        INotificationDefinitionManager definitionManager,
        INotificationDataSerializer dataSerializer)
    {
        Dispatcher = dispatcher;
        GuidGenerator = guidGenerator;
        Clock = clock;
        CurrentTenant = currentTenant;
        DefinitionManager = definitionManager;
        DataSerializer = dataSerializer;
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
            // The publish boundary: the payload is serialized here, once, and travels as a string from now on. An
            // unregistered payload type throws here, before anything is persisted or enqueued.
            DataJson = DataSerializer.Serialize(data),
            EntityTypeName = entityIdentifier?.EntityTypeName,
            EntityId = entityIdentifier?.EntityId,
            Severity = severity,
            CreationTime = Clock.Now,
            TenantId = CurrentTenant.Id
        };

        await Dispatcher.DispatchAsync(notification, userIds, excludedUserIds);
    }
}
