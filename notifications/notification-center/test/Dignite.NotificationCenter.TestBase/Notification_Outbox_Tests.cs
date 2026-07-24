using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>
/// Shared provider contract for transactional event boxes: notification persistence and the outgoing event
/// record commit or roll back as one unit.
/// </summary>
public abstract class Notification_Outbox_Tests<TStartupModule> : NotificationCenterTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    protected abstract Task<long> GetOutboxCountAsync();

    protected virtual Task AssertProviderTransactionActiveAsync()
    {
        return Task.CompletedTask;
    }

    protected static NotificationInfo NewNotification(Guid id, Guid? tenantId = null)
    {
        return new NotificationInfo
        {
            Id = id,
            NotificationName = "order.shipped",
            Data = new MessageNotificationData("hi"),
            Severity = NotificationSeverity.Info,
            CreationTime = DateTime.UtcNow,
            TenantId = tenantId
        };
    }

    [Fact]
    public async Task Distribution_persists_the_notification_and_the_outbox_event_together()
    {
        var notificationId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<INotificationDistributor>()
                .DistributeAsync(NewNotification(notificationId), new[] { Guid.NewGuid() });
            await AssertProviderTransactionActiveAsync();
        }, isTransactional: true);

        await WithUnitOfWorkAsync(async () =>
        {
            (await GetRequiredService<IRepository<Notification, Guid>>().FindAsync(notificationId)).ShouldNotBeNull();
            (await GetOutboxCountAsync()).ShouldBeGreaterThan(0);
        });
    }

    [Fact]
    public async Task Inline_distribution_persists_into_the_recorded_tenant()
    {
        var notificationId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        using (GetRequiredService<ICurrentTenant>().Change(tenantId, "tenant"))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await GetRequiredService<INotificationDistributor>()
                    .DistributeAsync(NewNotification(notificationId, tenantId), new[] { Guid.NewGuid() });
            }, isTransactional: true);

            await WithUnitOfWorkAsync(async () =>
            {
                var notification = await GetRequiredService<IRepository<Notification, Guid>>()
                    .FindAsync(notificationId);

                notification.ShouldNotBeNull();
                notification!.TenantId.ShouldBe(tenantId);
                (await GetOutboxCountAsync()).ShouldBeGreaterThan(0);
            });
        }
    }

    [Fact]
    public async Task Draining_the_event_boxes_round_trips_the_delivery_request_with_its_concrete_data()
    {
        var notificationId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<INotificationDistributor>()
                .DistributeAsync(NewNotification(notificationId), new[] { recipientId });
        }, isTransactional: true);

        var eventBusOptions = GetRequiredService<IOptions<AbpDistributedEventBusOptions>>().Value;
        var outboxConfig = eventBusOptions.Outboxes.Values.Single();
        var outbox = (IEventOutbox)ServiceProvider.GetRequiredService(outboxConfig.ImplementationType);

        var waitingEvents = new List<OutgoingEventInfo>();
        await WithUnitOfWorkAsync(async () =>
        {
            waitingEvents.AddRange(await outbox.GetWaitingEventsAsync(int.MaxValue));
        });
        var deliveryEvent = waitingEvents
            .Single(waiting => waiting.EventName == "Dignite.Abp.Notifications.NotificationDeliveryRequested");

        // ABP wrote these bytes with plain System.Text.Json and no app-level options. The wire envelope must
        // still carry the stable discriminator and never a CLR type name (invariants §1).
        var wireJson = Encoding.UTF8.GetString(deliveryEvent.EventData);
        wireJson.ShouldContain("Dignite.Message");
        wireJson.ShouldNotContain(nameof(MessageNotificationData));
        wireJson.ShouldNotContain("Version=");

        // Drain the outbox exactly as ABP's OutboxSender does — the deserialize inside this call is what a
        // live NotificationData property on the ETO used to break. With an inbox configured the event hops
        // into the inbox instead of firing handlers directly, so drain that too (the second plain-STJ read).
        var eventBus = (ISupportsEventBoxes)GetRequiredService<IDistributedEventBus>();
        await WithUnitOfWorkAsync(() => eventBus.PublishManyFromOutboxAsync(new[] { deliveryEvent }, outboxConfig));

        var inboxConfig = eventBusOptions.Inboxes.Values.Single();
        var inbox = (IEventInbox)ServiceProvider.GetRequiredService(inboxConfig.ImplementationType);
        await WithUnitOfWorkAsync(async () =>
        {
            foreach (var incomingEvent in await inbox.GetWaitingEventsAsync(int.MaxValue))
            {
                await eventBus.ProcessFromInboxAsync(incomingEvent, inboxConfig);
            }
        });

        var received = GetRequiredService<ReceivedNotificationDeliveries>().Items.ShouldHaveSingleItem();
        received.UserId.ShouldBe(recipientId);
        received.NotificationId.ShouldBe(notificationId);

        // Hydrate exactly as a notifier does; the concrete payload type and its members must survive.
        var payload = NotificationPayload.FromRequest(
            received,
            GetRequiredService<INotificationDataSerializer>());
        payload.Data.ShouldBeOfType<MessageNotificationData>().Message.ShouldBe("hi");
    }

    [Fact]
    public async Task A_rolled_back_unit_of_work_leaves_neither_the_notification_nor_the_event()
    {
        var notificationId = Guid.NewGuid();

        using (var unitOfWork = GetRequiredService<IUnitOfWorkManager>().Begin(
                   requiresNew: true,
                   isTransactional: true))
        {
            await GetRequiredService<INotificationStore>().InsertNotificationAsync(NewNotification(notificationId));

            var eventBusOptions = GetRequiredService<IOptions<AbpDistributedEventBusOptions>>().Value;
            var outboxConfig = eventBusOptions.Outboxes.Values.Single();
            var outbox = (IEventOutbox)ServiceProvider.GetRequiredService(outboxConfig.ImplementationType);
            await outbox.EnqueueAsync(new OutgoingEventInfo(
                Guid.NewGuid(),
                "Dignite.Abp.Notifications.NotificationDeliveryRequested",
                Array.Empty<byte>(),
                DateTime.UtcNow));

            await unitOfWork.SaveChangesAsync();
            await AssertProviderTransactionActiveAsync();
            (await GetOutboxCountAsync()).ShouldBe(1);

            // No CompleteAsync: both already-flushed records must roll back.
        }

        await WithUnitOfWorkAsync(async () =>
        {
            (await GetRequiredService<IRepository<Notification, Guid>>().FindAsync(notificationId)).ShouldBeNull();
            (await GetOutboxCountAsync()).ShouldBe(0);
        });
    }
}
