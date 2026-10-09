using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace Dignite.Abp.Notifications.Client;

/// <summary>
/// Publishes a notification for another process to distribute. What only this process knows is decided here; the rest
/// is left to the receiver:
/// <list type="number">
/// <item>The definition must exist locally — an undefined name throws while the caller is still on the line, as the
/// local publisher does. The business module's definition provider runs in this process.</item>
/// <item>The payload is serialized once (<see cref="INotificationDataSerializer"/>), so the receiver needs no payload
/// type.</item>
/// <item>The channels are resolved here with <see cref="INotificationChannelResolver"/>: the routing rules (module
/// defaults and host overrides in <see cref="NotificationRoutingOptions"/>) are configured in this process.</item>
/// <item>One <see cref="NotificationPublishRequestedEto"/> is published. With an ambient unit of work it goes into this
/// process's outbox, in the same transaction as the business change; without one it is sent directly.</item>
/// </list>
/// Recipients are neither deduplicated nor counted here: one notification is always one event, and the receiver decides
/// between inline distribution and a background job.
/// </summary>
/// <remarks>
/// Registered with <c>TryRegister</c>: the local <c>DefaultNotificationPublisher</c> of <c>Dignite.Abp.Notifications</c>,
/// registered plainly, wins over it in either module order, and it wins over <see cref="NullNotificationPublisher"/>, the
/// fallback registered only when no module registered a publisher.
/// </remarks>
[Dependency(TryRegister = true)]
[ExposeServices(typeof(INotificationPublisher), typeof(RemoteNotificationPublisher))]
public class RemoteNotificationPublisher : INotificationPublisher, ITransientDependency
{
    protected INotificationDefinitionManager DefinitionManager { get; }

    protected INotificationChannelResolver ChannelResolver { get; }

    protected INotificationDataSerializer DataSerializer { get; }

    protected IDistributedEventBus DistributedEventBus { get; }

    protected IGuidGenerator GuidGenerator { get; }

    protected IClock Clock { get; }

    protected ICurrentTenant CurrentTenant { get; }

    public RemoteNotificationPublisher(
        INotificationDefinitionManager definitionManager,
        INotificationChannelResolver channelResolver,
        INotificationDataSerializer dataSerializer,
        IDistributedEventBus distributedEventBus,
        IGuidGenerator guidGenerator,
        IClock clock,
        ICurrentTenant currentTenant)
    {
        DefinitionManager = definitionManager;
        ChannelResolver = channelResolver;
        DataSerializer = dataSerializer;
        DistributedEventBus = distributedEventBus;
        GuidGenerator = guidGenerator;
        Clock = clock;
        CurrentTenant = currentTenant;
    }

    public virtual async Task PublishAsync(
        string notificationName,
        NotificationData? data = null,
        NotificationEntityIdentifier? entityIdentifier = null,
        NotificationSeverity severity = NotificationSeverity.Info,
        Guid[]? userIds = null,
        Guid[]? excludedUserIds = null)
    {
        // An explicitly empty recipient list is a no-op here too: nothing is sent.
        if (userIds is { Length: 0 })
        {
            return;
        }

        var definition = await DefinitionManager.GetAsync(notificationName);

        var notification = new NotificationInfo
        {
            Id = GuidGenerator.Create(),
            NotificationName = notificationName,
            DataJson = DataSerializer.Serialize(data),
            EntityTypeName = entityIdentifier?.EntityTypeName,
            EntityId = entityIdentifier?.EntityId,
            Severity = severity,
            CreationTime = Clock.Now,
            TenantId = CurrentTenant.Id
        };

        // The resolver runs in the notification's tenant, which here is the ambient one it was taken from.
        notification.Channels = await ResolveChannelsOrNullAsync(definition, notification);

        await DistributedEventBus.PublishAsync(CreateEventData(notification, userIds, excludedUserIds));
    }

    /// <summary>
    /// The channels as the distributor would compute them: trimmed and de-duplicated ignoring case, null for
    /// inbox-only. A blank name is a configuration error of this process and fails here rather than in the receiver.
    /// </summary>
    protected virtual async Task<string[]?> ResolveChannelsOrNullAsync(
        NotificationDefinition definition,
        NotificationInfo notification)
    {
        var channels = await ChannelResolver.ResolveAsync(definition, notification);
        if (channels == null || channels.Length == 0)
        {
            return null;
        }

        if (channels.Any(string.IsNullOrWhiteSpace))
        {
            throw new AbpException(
                $"Notification '{notification.NotificationName}' has invalid delivery channel configuration.");
        }

        return channels.Select(channel => channel.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    protected virtual NotificationPublishRequestedEto CreateEventData(
        NotificationInfo notification,
        Guid[]? userIds,
        Guid[]? excludedUserIds)
    {
        return new NotificationPublishRequestedEto
        {
            NotificationId = notification.Id,
            TenantId = notification.TenantId,
            NotificationName = notification.NotificationName,
            DataJson = notification.DataJson,
            Severity = notification.Severity,
            EntityTypeName = notification.EntityTypeName,
            EntityId = notification.EntityId,
            CreationTime = notification.CreationTime,
            UserIds = userIds,
            ExcludedUserIds = excludedUserIds,
            Channels = notification.Channels
        };
    }
}
