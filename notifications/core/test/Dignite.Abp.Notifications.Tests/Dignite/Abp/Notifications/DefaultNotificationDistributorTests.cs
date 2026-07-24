using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Abp.Notifications;

public class DefaultNotificationDistributorTests
{
    [Fact]
    public async Task Publishes_eto_once_per_distinct_explicit_user_minus_excluded()
    {
        var store = Substitute.For<INotificationStore>();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        var eventBus = Substitute.For<IDistributedEventBus>();
        definitionManager.Get("test").Returns(DefinitionWithChannels());
        definitionManager.IsAvailableAsync("test", Arg.Any<Guid>()).Returns(true);

        var published = new List<NotificationDeliveryRequestedEto>();
        eventBus.WhenForAnyArgs(x => x.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(ci => published.Add(ci.Arg<NotificationDeliveryRequestedEto>()));

        var distributor = CreateDistributor(store, definitionManager, eventBus);

        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        var u3 = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var notification = new NotificationInfo
        {
            Id = Guid.NewGuid(),
            NotificationName = "test",
            CreationTime = DateTime.UtcNow,
            TenantId = tenantId
        };

        await distributor.DistributeAsync(notification, new[] { u1, u1, u2, u3, u3 }, new[] { u2 });

        published.Count.ShouldBe(2);
        published.Select(item => item.UserId).ShouldBe(new[] { u1, u3 }, ignoreOrder: true);
        published.ShouldAllBe(item => item.TenantId == tenantId);
        ((IEventDataMayHaveTenantId)published[0]).IsMultiTenant(out var eventTenantId).ShouldBeTrue();
        eventTenantId.ShouldBe(tenantId);

        await store.Received(1).InsertNotificationAsync(
            Arg.Any<NotificationInfo>(),
            Arg.Any<CancellationToken>());
        // The distributor also carries the notification tenant explicitly on every persisted per-user row and ETO.
        await store.Received(1).InsertUserNotificationsAsync(
            Arg.Is<IReadOnlyCollection<UserNotificationInfo>>(rows =>
                rows.Count == 2 && rows.All(row => row.TenantId == tenantId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Explicit_recipients_are_processed_in_bounded_batches()
    {
        var store = Substitute.For<INotificationStore>();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        var eventBus = Substitute.For<IDistributedEventBus>();
        var notificationName = $"batch-{Guid.NewGuid():N}";
        definitionManager.Get(notificationName).Returns(
            new NotificationDefinition(notificationName, new FixedLocalizableString("Batch")).UseChannels("Test"));
        definitionManager.IsAvailableAsync(notificationName, Arg.Any<Guid>()).Returns(true);

        var writeBatches = new List<Guid[]>();
        store.When(storeCapability => storeCapability.InsertUserNotificationsAsync(
                Arg.Any<IReadOnlyCollection<UserNotificationInfo>>(),
                Arg.Any<CancellationToken>()))
            .Do(call => writeBatches.Add(call
                .ArgAt<IReadOnlyCollection<UserNotificationInfo>>(0)
                .Select(row => row.UserId)
                .ToArray()));

        var deliveryRequests = new List<NotificationDeliveryRequestedEto>();
        eventBus.WhenForAnyArgs(bus => bus.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(call => deliveryRequests.Add(call.Arg<NotificationDeliveryRequestedEto>()));

        var distributor = CreateDistributor(
            store,
            definitionManager,
            eventBus,
            options: new NotificationDistributionOptions { RecipientBatchSize = 128 });
        var distinctUserIds = Enumerable.Range(0, 512).Select(_ => Guid.NewGuid()).ToArray();
        var excludedUserId = Guid.NewGuid();
        var userIds = distinctUserIds
            .Append(excludedUserId)
            .Concat(new[] { distinctUserIds[0], distinctUserIds[255], distinctUserIds[^1] })
            .ToArray();

        await distributor.DistributeAsync(
            new NotificationInfo { Id = Guid.NewGuid(), NotificationName = notificationName },
            userIds,
            new[] { excludedUserId });

        writeBatches.Count.ShouldBe(4);
        writeBatches.ShouldAllBe(batch => batch.Length <= 128);
        writeBatches.SelectMany(batch => batch).Distinct().Count().ShouldBe(512);
        deliveryRequests.Count.ShouldBe(512);
        deliveryRequests.Select(item => item.UserId).Distinct().Count().ShouldBe(512);
        deliveryRequests.Select(item => item.UserId).ShouldNotContain(excludedUserId);
        await store.Received(1).InsertNotificationAsync(
            Arg.Any<NotificationInfo>(),
            Arg.Any<CancellationToken>());
        await store.DidNotReceiveWithAnyArgs().InsertUserNotificationAsync(default!);
    }

    [Fact]
    public async Task Cancellation_is_observed_during_delivery_publication()
    {
        var store = Substitute.For<INotificationStore>();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        var eventBus = Substitute.For<IDistributedEventBus>();
        definitionManager.Get("test").Returns(DefinitionWithChannels());
        definitionManager.IsAvailableAsync("test", Arg.Any<Guid>()).Returns(true);

        var persistedRecipients = new List<Guid>();
        store.When(storeCapability => storeCapability.InsertUserNotificationsAsync(
                Arg.Any<IReadOnlyCollection<UserNotificationInfo>>(),
                Arg.Any<CancellationToken>()))
            .Do(call => persistedRecipients.AddRange(call
                .ArgAt<IReadOnlyCollection<UserNotificationInfo>>(0)
                .Select(row => row.UserId)));
        using var cancellation = new CancellationTokenSource();
        var deliveryCount = 0;
        eventBus.WhenForAnyArgs(bus => bus.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(_ =>
            {
                deliveryCount++;
                cancellation.Cancel();
            });
        var distributor = CreateDistributor(
            store,
            definitionManager,
            eventBus,
            options: new NotificationDistributionOptions { RecipientBatchSize = 2 });

        await Should.ThrowAsync<OperationCanceledException>(() => distributor.DistributeAsync(
            new NotificationInfo { Id = Guid.NewGuid(), NotificationName = "test" },
            Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray(),
            null,
            cancellation.Token));

        deliveryCount.ShouldBe(1);
        persistedRecipients.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(NotificationDistributionOptions.MaxBatchSize + 1)]
    public void Invalid_distribution_batch_size_fails_before_distribution(int batchSize)
    {
        var options = new NotificationDistributionOptions { RecipientBatchSize = batchSize };

        var exception = Should.Throw<InvalidOperationException>(() => CreateDistributor(
            Substitute.For<INotificationStore>(),
            Substitute.For<INotificationDefinitionManager>(),
            Substitute.For<IDistributedEventBus>(),
            options: options));

        exception.Message.ShouldContain(nameof(NotificationDistributionOptions.RecipientBatchSize));
    }

    [Fact]
    public void Inline_threshold_cannot_exceed_the_batch_safeguard()
    {
        var options = new NotificationDistributionOptions
        {
            DirectDistributionUserThreshold = NotificationDistributionOptions.MaxBatchSize + 1
        };

        var exception = Should.Throw<InvalidOperationException>(() => CreateDistributor(
            Substitute.For<INotificationStore>(),
            Substitute.For<INotificationDefinitionManager>(),
            Substitute.For<IDistributedEventBus>(),
            options: options));

        exception.Message.ShouldContain(nameof(NotificationDistributionOptions.DirectDistributionUserThreshold));
    }

    [Fact]
    public async Task Empty_explicit_recipient_list_does_not_resolve_subscriptions_or_produce_side_effects()
    {
        var store = Substitute.For<INotificationStore>();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        var eventBus = Substitute.For<IDistributedEventBus>();
        definitionManager.Get("test").Returns(DefinitionWithChannels());

        var subscribedUser = Guid.NewGuid();
        store.GetSubscriptionUserIdsAsync(
                "test", null, null, Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { subscribedUser });
        definitionManager.IsAvailableAsync("test", subscribedUser).Returns(true);

        var distributor = CreateDistributor(store, definitionManager, eventBus);
        var notification = new NotificationInfo { Id = Guid.NewGuid(), NotificationName = "test" };

        await distributor.DistributeAsync(notification, Array.Empty<Guid>());

        definitionManager.DidNotReceiveWithAnyArgs().Get(default!);
        await store.DidNotReceiveWithAnyArgs().GetSubscriptionUserIdsAsync(
            default!, default, default, default, default, default);
        await store.DidNotReceiveWithAnyArgs().InsertNotificationAsync(default!);
        await store.DidNotReceiveWithAnyArgs().InsertUserNotificationsAsync(default!, default);
        await eventBus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>());
    }

    [Fact]
    public async Task Resolves_subscribers_and_keeps_only_available_ones()
    {
        var store = Substitute.For<INotificationStore>();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        var eventBus = Substitute.For<IDistributedEventBus>();
        definitionManager.Get("test").Returns(DefinitionWithChannels());

        var available = Guid.NewGuid();
        var notAvailable = Guid.NewGuid();
        store.GetSubscriptionUserIdsAsync(
                "test", null, null, Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Guid?>(3).HasValue
                ? new List<Guid>()
                : new List<Guid> { available, notAvailable });
        definitionManager.IsAvailableAsync("test", available).Returns(true);
        definitionManager.IsAvailableAsync("test", notAvailable).Returns(false);

        NotificationDeliveryRequestedEto? published = null;
        eventBus.WhenForAnyArgs(x => x.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(ci => published = ci.Arg<NotificationDeliveryRequestedEto>());

        var distributor = CreateDistributor(store, definitionManager, eventBus);
        var notification = new NotificationInfo { Id = Guid.NewGuid(), NotificationName = "test" };

        // No explicit userIds → subscription-driven path, filtered by availability.
        await distributor.DistributeAsync(notification);

        published.ShouldNotBeNull();
        published!.UserId.ShouldBe(available);
    }

    [Fact]
    public async Task Does_nothing_when_there_are_no_target_users()
    {
        var store = Substitute.For<INotificationStore>();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        var eventBus = Substitute.For<IDistributedEventBus>();
        definitionManager.Get("test").Returns(DefinitionWithChannels());

        var distributor = CreateDistributor(store, definitionManager, eventBus);
        var notification = new NotificationInfo { Id = Guid.NewGuid(), NotificationName = "test" };

        // Explicit single user, then excluded → empty target set.
        var user = Guid.NewGuid();
        await distributor.DistributeAsync(notification, new[] { user }, new[] { user });

        await store.DidNotReceiveWithAnyArgs().InsertNotificationAsync(default!);
        await eventBus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>());
    }

    [Fact]
    public async Task No_channels_persists_without_publishing_a_delivery_event()
    {
        var store = Substitute.For<INotificationStore>();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        var eventBus = Substitute.For<IDistributedEventBus>();
        definitionManager.Get("test").Returns(new NotificationDefinition("test", new FixedLocalizableString("Test")));
        definitionManager.IsAvailableAsync("test", Arg.Any<Guid>()).Returns(true);

        var userId = Guid.NewGuid();
        var distributor = CreateDistributor(store, definitionManager, eventBus);
        var notification = new NotificationInfo { Id = Guid.NewGuid(), NotificationName = "test" };

        await distributor.DistributeAsync(notification, new[] { userId });

        await store.Received(1).InsertNotificationAsync(notification);
        await store.Received(1).InsertUserNotificationsAsync(
            Arg.Is<IReadOnlyCollection<UserNotificationInfo>>(rows =>
                rows.Count == 1 && rows.Single().UserId == userId),
            Arg.Any<CancellationToken>());
        await eventBus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>());
    }

    [Fact]
    public async Task No_channels_throw_when_there_is_no_inbox_store()
    {
        var store = new NullNotificationStore();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        var eventBus = Substitute.For<IDistributedEventBus>();
        definitionManager.Get("test").Returns(new NotificationDefinition("test", new FixedLocalizableString("Test")));

        var distributor = CreateDistributor(store, definitionManager, eventBus);
        var notification = new NotificationInfo { Id = Guid.NewGuid(), NotificationName = "test" };

        await Should.ThrowAsync<AbpException>(() => distributor.DistributeAsync(notification, new[] { Guid.NewGuid() }));

        await eventBus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>());
    }

    [Fact]
    public async Task Carries_entity_identity_onto_the_delivery_event_and_the_per_user_view()
    {
        var store = Substitute.For<INotificationStore>();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        var eventBus = Substitute.For<IDistributedEventBus>();
        definitionManager.Get("test").Returns(DefinitionWithChannels());
        definitionManager.IsAvailableAsync("test", Arg.Any<Guid>()).Returns(true);

        NotificationDeliveryRequestedEto? published = null;
        eventBus.WhenForAnyArgs(x => x.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(ci => published = ci.Arg<NotificationDeliveryRequestedEto>());

        var distributor = CreateDistributor(store, definitionManager, eventBus);

        await distributor.DistributeAsync(
            new NotificationInfo
            {
                Id = Guid.NewGuid(),
                NotificationName = "test",
                EntityTypeName = "Demo.Order",
                EntityId = "1001"
            },
            new[] { Guid.NewGuid(), Guid.NewGuid() });

        // Without this, a notifier cannot tell which entity a notification is about.
        published.ShouldNotBeNull();
        published!.EntityTypeName.ShouldBe("Demo.Order");
        published.EntityId.ShouldBe("1001");

        // The per-user view forwards entity identity but must still drop the recipient list (invariants §4).
        var payload = NotificationPayload.FromRequest(published, NotificationTestObjects.CreateSerializer());
        payload.EntityTypeName.ShouldBe("Demo.Order");
        payload.EntityId.ShouldBe("1001");
        typeof(NotificationPayload).GetProperty("UserIds").ShouldBeNull();
    }

    [Fact]
    public async Task Explicit_recipients_are_filtered_by_the_same_definition_eligibility_as_subscribers()
    {
        var store = Substitute.For<INotificationStore>();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        var eventBus = Substitute.For<IDistributedEventBus>();
        definitionManager.Get("test").Returns(DefinitionWithChannels());

        var eligible = Guid.NewGuid();
        var denied = Guid.NewGuid();
        definitionManager.IsAvailableAsync("test", eligible).Returns(true);
        definitionManager.IsAvailableAsync("test", denied).Returns(false);

        NotificationDeliveryRequestedEto? published = null;
        eventBus.WhenForAnyArgs(bus => bus.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(call => published = call.Arg<NotificationDeliveryRequestedEto>());

        var distributor = CreateDistributor(store, definitionManager, eventBus);
        await distributor.DistributeAsync(
            new NotificationInfo { Id = Guid.NewGuid(), NotificationName = "test" },
            new[] { eligible, denied });

        published.ShouldNotBeNull();
        published!.UserId.ShouldBe(eligible);
        await store.DidNotReceive().InsertUserNotificationsAsync(
            Arg.Is<IReadOnlyCollection<UserNotificationInfo>>(rows => rows.Any(row => row.UserId == denied)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Entire_distribution_uses_the_notification_tenant_or_host_and_restores_the_caller_context(
        bool hostNotification)
    {
        var store = Substitute.For<INotificationStore>();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        var eventBus = Substitute.For<IDistributedEventBus>();
        var currentTenant = new TestCurrentTenant();
        Guid? notificationTenantId = hostNotification ? null : Guid.NewGuid();
        var callerTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantsSeen = new List<Guid?>();
        definitionManager.Get("test").Returns(DefinitionWithChannels());
        store.GetSubscriptionUserIdsAsync(
                "test", null, null, Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
        {
            tenantsSeen.Add(currentTenant.Id);
            return call.ArgAt<Guid?>(3).HasValue ? new List<Guid>() : new List<Guid> { userId };
        });
        definitionManager.IsAvailableAsync("test", userId).Returns(_ =>
        {
            tenantsSeen.Add(currentTenant.Id);
            return true;
        });
        store.When(candidate => candidate.InsertNotificationAsync(Arg.Any<NotificationInfo>()))
            .Do(_ => tenantsSeen.Add(currentTenant.Id));
        store.When(candidate => candidate.InsertUserNotificationsAsync(
                Arg.Any<IReadOnlyCollection<UserNotificationInfo>>(),
                Arg.Any<CancellationToken>()))
            .Do(_ => tenantsSeen.Add(currentTenant.Id));
        eventBus.WhenForAnyArgs(candidate => candidate.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(_ => tenantsSeen.Add(currentTenant.Id));
        var distributor = CreateDistributor(
            store,
            definitionManager,
            eventBus,
            currentTenant: currentTenant);

        using (currentTenant.Change(callerTenantId, "caller"))
        {
            await distributor.DistributeAsync(new NotificationInfo
            {
                Id = Guid.NewGuid(),
                NotificationName = "test",
                TenantId = notificationTenantId
            });

            currentTenant.Id.ShouldBe(callerTenantId);
        }

        tenantsSeen.Count.ShouldBe(5);
        tenantsSeen.ShouldAllBe(tenantId => tenantId == notificationTenantId);
        currentTenant.Id.ShouldBeNull();
    }

    private static DefaultNotificationDistributor CreateDistributor(
        INotificationStore store,
        INotificationDefinitionManager definitionManager,
        IDistributedEventBus eventBus,
        ICurrentTenant? currentTenant = null,
        ILogger<DefaultNotificationDistributor>? logger = null,
        NotificationDistributionOptions? options = null)
    {
        return new DefaultNotificationDistributor(
            store,
            definitionManager,
            eventBus,
            NotificationTestObjects.CreateSerializer(),
            currentTenant ?? new TestCurrentTenant(),
            logger ?? NullLogger<DefaultNotificationDistributor>.Instance,
            Options.Create(options ?? new NotificationDistributionOptions()));
    }

    private static NotificationDefinition DefinitionWithChannels()
    {
        return new NotificationDefinition("test", new FixedLocalizableString("Test")).UseChannels("Test");
    }
}
