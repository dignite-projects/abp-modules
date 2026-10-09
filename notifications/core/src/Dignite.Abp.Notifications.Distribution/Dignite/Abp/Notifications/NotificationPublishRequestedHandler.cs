using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Distributes a notification published in another process (by <c>Dignite.Abp.Notifications.Remote</c>) with this
/// process's distributor, exactly as a local publish would: an explicit fan-out of at most
/// <see cref="NotificationDistributionOptions.DirectDistributionUserThreshold"/> users inline, anything else through this
/// process's own <see cref="NotificationDistributionJob"/>.
/// </summary>
/// <remarks>
/// <para>
/// The notification keeps what the publisher decided: its id, its tenant (the event is <c>IMultiTenant</c> and the id
/// and tenant are also set explicitly on the <see cref="NotificationInfo"/>), its payload JSON as it is, and its
/// channels — resolved where the routing rules live, so this process's resolver is not asked again.
/// </para>
/// <para>
/// Idempotency is ABP's: the event inbox deduplicates by message id, and the handler runs inside the inbox's
/// transactional unit of work, which also holds the inbox rows and the outbox records of the delivery events. Without an
/// inbox the handler opens its own unit of work (it joins the inbox's when there is one). The store's check on the
/// notification id is a second guard.
/// </para>
/// </remarks>
[ExposeServices(
    typeof(IDistributedEventHandler<NotificationPublishRequestedEto>),
    typeof(NotificationPublishRequestedHandler))]
public class NotificationPublishRequestedHandler :
    IDistributedEventHandler<NotificationPublishRequestedEto>,
    ITransientDependency
{
    protected NotificationDistributionDispatcher Dispatcher { get; }

    protected IUnitOfWorkManager UnitOfWorkManager { get; }

    protected AbpUnitOfWorkDefaultOptions UnitOfWorkDefaultOptions { get; }

    protected ICancellationTokenProvider CancellationTokenProvider { get; }

    public NotificationPublishRequestedHandler(
        NotificationDistributionDispatcher dispatcher,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<AbpUnitOfWorkDefaultOptions> unitOfWorkDefaultOptions,
        ICancellationTokenProvider cancellationTokenProvider)
    {
        Dispatcher = dispatcher;
        UnitOfWorkManager = unitOfWorkManager;
        UnitOfWorkDefaultOptions = unitOfWorkDefaultOptions.Value;
        CancellationTokenProvider = cancellationTokenProvider;
    }

    public virtual async Task HandleEventAsync(NotificationPublishRequestedEto eventData)
    {
        var notification = CreateNotification(eventData);
        var cancellationToken = CancellationTokenProvider.Token;

        // Transactional as ABP's [UnitOfWork] would make it for a write, unless the host turned transactions off.
        using var unitOfWork = UnitOfWorkManager.Begin(
            requiresNew: false,
            isTransactional: UnitOfWorkDefaultOptions.CalculateIsTransactional(autoValue: true));

        await Dispatcher.DispatchAsync(notification, eventData.UserIds, eventData.ExcludedUserIds, cancellationToken);

        await unitOfWork.CompleteAsync(cancellationToken);
    }

    protected virtual NotificationInfo CreateNotification(NotificationPublishRequestedEto eventData)
    {
        return new NotificationInfo
        {
            Id = eventData.NotificationId,
            NotificationName = eventData.NotificationName,
            DataJson = eventData.DataJson,
            EntityTypeName = eventData.EntityTypeName,
            EntityId = eventData.EntityId,
            Severity = eventData.Severity,
            CreationTime = eventData.CreationTime,
            // Explicit, never the ambient tenant: null is the host.
            TenantId = eventData.TenantId,
            // The publisher already resolved them; null on the event means inbox-only, which here is an empty array
            // (null would ask the distributor to resolve them again).
            Channels = eventData.Channels ?? Array.Empty<string>()
        };
    }
}
