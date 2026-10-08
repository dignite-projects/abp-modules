using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Gdpr;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Dignite.NotificationCenter;

/// <summary>
/// Erases the personal data this module keeps about a user when a <see cref="GdprUserDataDeletionRequestedEto"/>
/// arrives: the inbox (<see cref="UserNotification"/>), the notification subscriptions, and the push device
/// registrations.
/// </summary>
/// <remarks>
/// Before ABP 10.7 the event carries only the user id — no tenant — so the event bus cannot enter the user's tenant,
/// and a distributed consumer runs in whatever tenant happens to be ambient (the host, usually). The user id is a
/// globally unique identifier, so the deletes go by it alone and reach every tenant in the database; no other user's
/// rows can match. A host with a database per tenant still has to make the event reach the right tenant database;
/// nothing in a library can do that for it. From ABP 10.7 the event also names its tenant and the bus enters it;
/// deleting by user id stays correct there.
/// <para>
/// The shared <see cref="Notification"/> payload is not touched: it has no owner and is referenced by every
/// recipient's inbox row, so removing orphaned payloads is the host's retention job (see "Retention and lifecycle
/// cleanup" in the README).
/// </para>
/// <para>
/// Every delete is idempotent, so a redelivered event simply finishes the job. The three deletes share one unit of
/// work, but that is atomic only where the host runs units of work in a transaction (not on a standalone MongoDB), so
/// completion rests on redelivery: the ABP event inbox retries a failed event, while a bus without an inbox hands
/// the exception to the publisher. All inbox rows go regardless of read state: the "never delete <c>Unread</c>"
/// retention rule is about ageing out rows, not about erasing a person.
/// </para>
/// <para>
/// Nothing in this repository publishes the event — the host needs a publisher, such as a GDPR module that raises it
/// or its own code through <c>IDistributedEventBus</c>. Without one this handler is never invoked.
/// </para>
/// </remarks>
[ExposeServices(
    typeof(IDistributedEventHandler<GdprUserDataDeletionRequestedEto>),
    typeof(GdprUserDataDeletionRequestedHandler))]
public class GdprUserDataDeletionRequestedHandler :
    IDistributedEventHandler<GdprUserDataDeletionRequestedEto>,
    ITransientDependency
{
    protected INotificationStore Store { get; }
    protected PushDeviceManager PushDeviceManager { get; }
    protected ICancellationTokenProvider CancellationTokenProvider { get; }

    public GdprUserDataDeletionRequestedHandler(
        INotificationStore store,
        PushDeviceManager pushDeviceManager,
        ICancellationTokenProvider cancellationTokenProvider)
    {
        Store = store;
        PushDeviceManager = pushDeviceManager;
        CancellationTokenProvider = cancellationTokenProvider;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(GdprUserDataDeletionRequestedEto eventData)
    {
        var cancellationToken = CancellationTokenProvider.Token;

        await Store.DeleteAllUserDataAsync(eventData.UserId, cancellationToken);
        await PushDeviceManager.RemoveAllAsync(eventData.UserId, cancellationToken);
    }
}
