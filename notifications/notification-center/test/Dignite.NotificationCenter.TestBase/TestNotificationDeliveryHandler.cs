using System.Collections.Concurrent;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace Dignite.NotificationCenter;

/// <summary>
/// Collects every <see cref="NotificationDeliveryRequestedEto"/> that actually reaches a distributed event
/// handler, so event-box tests can assert on what a real consumer receives after a box is drained.
/// </summary>
public class ReceivedNotificationDeliveries : ISingletonDependency
{
    public ConcurrentQueue<NotificationDeliveryRequestedEto> Items { get; } = new();
}

[ExposeServices(
    typeof(IDistributedEventHandler<NotificationDeliveryRequestedEto>),
    typeof(TestNotificationDeliveryHandler))]
public class TestNotificationDeliveryHandler :
    IDistributedEventHandler<NotificationDeliveryRequestedEto>,
    ITransientDependency
{
    private readonly ReceivedNotificationDeliveries _received;

    public TestNotificationDeliveryHandler(ReceivedNotificationDeliveries received)
    {
        _received = received;
    }

    public Task HandleEventAsync(NotificationDeliveryRequestedEto eventData)
    {
        _received.Items.Enqueue(eventData);
        return Task.CompletedTask;
    }
}
