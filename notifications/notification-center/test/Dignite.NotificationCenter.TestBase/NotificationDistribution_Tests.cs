using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>
/// Provider-agnostic recipient-semantics scenarios. Running the same direct and background distribution
/// paths against both stores prevents EF Core and MongoDB from drifting on persistence or deduplication.
/// </summary>
public abstract class NotificationDistribution_Tests<TStartupModule> : NotificationCenterTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static NotificationInfo NewNotification(
        Guid id,
        string? entityTypeName = null,
        string? entityId = null)
    {
        return new NotificationInfo
        {
            Id = id,
            NotificationName = "order.shipped",
            Data = new MessageNotificationData("hi"),
            EntityTypeName = entityTypeName,
            EntityId = entityId,
            Severity = NotificationSeverity.Info,
            CreationTime = DateTime.UtcNow
        };
    }

    private DefaultNotificationDistributor CreateDistributor(
        IDistributedEventBus eventBus,
        NotificationDistributionOptions? options = null)
    {
        return new DefaultNotificationDistributor(
            GetRequiredService<INotificationStore>(),
            GetRequiredService<INotificationDefinitionManager>(),
            eventBus,
            GetRequiredService<INotificationDataSerializer>(),
            GetRequiredService<ICurrentTenant>(),
            GetRequiredService<ILogger<DefaultNotificationDistributor>>(),
            Options.Create(options ?? new NotificationDistributionOptions()));
    }

    private Task DistributeAsync(
        bool background,
        DefaultNotificationDistributor distributor,
        NotificationInfo notification,
        Guid[]? userIds)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            if (background)
            {
                var job = new NotificationDistributionJob(
                    distributor,
                    GetRequiredService<ICurrentTenant>(),
                    GetRequiredService<IUnitOfWorkManager>());
                await job.ExecuteAsync(new NotificationDistributionJobArgs(notification, userIds, null));
            }
            else
            {
                await distributor.DistributeAsync(notification, userIds);
            }
        });
    }

    private async Task InsertSubscriptionAsync(
        Guid userId,
        string? entityTypeName = null,
        string? entityId = null)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<INotificationStore>().InsertSubscriptionAsync(
                new NotificationSubscriptionInfo
                {
                    UserId = userId,
                    NotificationName = "order.shipped",
                    EntityTypeName = entityTypeName,
                    EntityId = entityId
                });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Null_recipient_list_resolves_subscriptions_on_direct_and_background_paths(bool background)
    {
        var subscriber = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        await InsertSubscriptionAsync(subscriber);

        var eventBus = Substitute.For<IDistributedEventBus>();
        NotificationDeliveryRequestedEto? published = null;
        eventBus.WhenForAnyArgs(x => x.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(call => published = call.Arg<NotificationDeliveryRequestedEto>());

        await DistributeAsync(background, CreateDistributor(eventBus), NewNotification(notificationId), null);

        await WithUnitOfWorkAsync(async () =>
        {
            (await GetRequiredService<INotificationStore>()
                .GetUserNotificationCountAsync(subscriber)).ShouldBe(1);
        });
        published.ShouldNotBeNull();
        published!.UserId.ShouldBe(subscriber);
        await eventBus.Received(1).PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_explicit_list_is_a_no_op_on_direct_and_background_paths(bool background)
    {
        var subscriber = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        await InsertSubscriptionAsync(subscriber);

        var eventBus = Substitute.For<IDistributedEventBus>();
        await DistributeAsync(
            background,
            CreateDistributor(eventBus),
            NewNotification(notificationId),
            Array.Empty<Guid>());

        await WithUnitOfWorkAsync(async () =>
        {
            (await GetRequiredService<IRepository<Notification, Guid>>()
                .FindAsync(notificationId)).ShouldBeNull();
            (await GetRequiredService<INotificationStore>()
                .GetUserNotificationCountAsync(subscriber)).ShouldBe(0);
        });
        await eventBus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Duplicate_explicit_users_produce_one_inbox_row_and_delivery_per_user(bool background)
    {
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var eventBus = Substitute.For<IDistributedEventBus>();
        var published = new List<NotificationDeliveryRequestedEto>();
        eventBus.WhenForAnyArgs(x => x.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(call => published.Add(call.Arg<NotificationDeliveryRequestedEto>()));

        await DistributeAsync(
            background,
            CreateDistributor(eventBus),
            NewNotification(notificationId),
            new[] { u1, u1, u2, u2 });

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            (await store.GetUserNotificationCountAsync(u1)).ShouldBe(1);
            (await store.GetUserNotificationCountAsync(u2)).ShouldBe(1);
        });
        published.Select(item => item.UserId).ShouldBe(new[] { u1, u2 }, ignoreOrder: true);
        published.Select(item => item.UserId).Distinct().Count().ShouldBe(2);
        await eventBus.Received(2).PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Entity_distribution_uses_definition_wide_fallback_and_exact_scope_once_per_user(bool background)
    {
        var definitionWide = Guid.NewGuid();
        var exact = Guid.NewGuid();
        var otherEntity = Guid.NewGuid();
        var both = Guid.NewGuid();

        await InsertSubscriptionAsync(definitionWide);
        await InsertSubscriptionAsync(exact, "Demo.Order", "42");
        await InsertSubscriptionAsync(otherEntity, "Demo.Order", "99");
        await InsertSubscriptionAsync(both);
        await InsertSubscriptionAsync(both, "Demo.Order", "42");

        var eventBus = Substitute.For<IDistributedEventBus>();
        var published = new List<NotificationDeliveryRequestedEto>();
        eventBus.WhenForAnyArgs(bus => bus.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(call => published.Add(call.Arg<NotificationDeliveryRequestedEto>()));

        await DistributeAsync(
            background,
            CreateDistributor(eventBus),
            NewNotification(Guid.NewGuid(), "Demo.Order", "42"),
            null);

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            (await store.GetUserNotificationCountAsync(definitionWide)).ShouldBe(1);
            (await store.GetUserNotificationCountAsync(exact)).ShouldBe(1);
            (await store.GetUserNotificationCountAsync(both)).ShouldBe(1);
            (await store.GetUserNotificationCountAsync(otherEntity)).ShouldBe(0);
        });

        published.Select(item => item.UserId)
            .ShouldBe(new[] { definitionWide, exact, both }, ignoreOrder: true);
        published.Select(item => item.UserId).Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task Direct_distribution_uses_the_notification_tenant_for_subscription_query_and_persistence()
    {
        var notificationTenantId = Guid.NewGuid();
        var callerTenantId = Guid.NewGuid();
        var notificationTenantSubscriber = Guid.NewGuid();
        var callerTenantSubscriber = Guid.NewGuid();
        var currentTenant = GetRequiredService<ICurrentTenant>();

        using (currentTenant.Change(notificationTenantId, "notification"))
        {
            await InsertSubscriptionAsync(notificationTenantSubscriber);
        }

        using (currentTenant.Change(callerTenantId, "caller"))
        {
            await InsertSubscriptionAsync(callerTenantSubscriber);
        }

        var eventBus = Substitute.For<IDistributedEventBus>();
        NotificationDeliveryRequestedEto? published = null;
        eventBus.WhenForAnyArgs(bus => bus.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(call => published = call.Arg<NotificationDeliveryRequestedEto>());
        var notification = NewNotification(Guid.NewGuid());
        notification.TenantId = notificationTenantId;

        using (currentTenant.Change(callerTenantId, "caller"))
        {
            await DistributeAsync(false, CreateDistributor(eventBus), notification, null);
            currentTenant.Id.ShouldBe(callerTenantId);
        }

        using (currentTenant.Change(notificationTenantId, "notification"))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                (await GetRequiredService<INotificationStore>()
                    .GetUserNotificationCountAsync(notificationTenantSubscriber)).ShouldBe(1);
            });
        }

        using (currentTenant.Change(callerTenantId, "caller"))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                (await GetRequiredService<INotificationStore>()
                    .GetUserNotificationCountAsync(callerTenantSubscriber)).ShouldBe(0);
            });
        }

        published.ShouldNotBeNull();
        published!.TenantId.ShouldBe(notificationTenantId);
        published.UserId.ShouldBe(notificationTenantSubscriber);
        currentTenant.Id.ShouldBeNull();
    }

    [Fact]
    public async Task Thousands_of_duplicate_scope_subscribers_are_paged_persisted_and_published_in_bounded_batches()
    {
        const int recipientCount = 2001;
        var users = Enumerable.Range(0, recipientCount).Select(_ => Guid.NewGuid()).ToArray();
        var duplicateScopeUsers = users.Take(301).ToArray();
        var now = DateTime.UtcNow;

        await WithUnitOfWorkAsync(async () =>
        {
            var subscriptions = users.Select(userId => new NotificationSubscription(
                    Guid.NewGuid(),
                    userId,
                    "order.shipped",
                    null,
                    null,
                    now,
                    null))
                .Concat(duplicateScopeUsers.Select(userId => new NotificationSubscription(
                    Guid.NewGuid(),
                    userId,
                    "order.shipped",
                    "Demo.Order",
                    "42",
                    now,
                    null)))
                .ToList();
            await GetRequiredService<IRepository<NotificationSubscription, Guid>>()
                .InsertManyAsync(subscriptions);
        });

        var eventBus = Substitute.For<IDistributedEventBus>();
        var deliveryRequests = new List<NotificationDeliveryRequestedEto>();
        eventBus.WhenForAnyArgs(bus => bus.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(call => deliveryRequests.Add(call.Arg<NotificationDeliveryRequestedEto>()));
        var notificationId = Guid.NewGuid();
        var distributor = CreateDistributor(eventBus, new NotificationDistributionOptions
        {
            RecipientBatchSize = 256
        });

        await DistributeAsync(
            background: false,
            distributor,
            NewNotification(notificationId, "Demo.Order", "42"),
            userIds: null);

        await WithUnitOfWorkAsync(async () =>
        {
            var inboxRows = await GetRequiredService<IRepository<UserNotification, Guid>>()
                .GetListAsync(row => row.NotificationId == notificationId);
            inboxRows.Count.ShouldBe(recipientCount);
            inboxRows.Select(row => row.UserId).Distinct().Count().ShouldBe(recipientCount);
            (await GetRequiredService<IRepository<Notification, Guid>>()
                .FindAsync(notificationId)).ShouldNotBeNull();
        });

        deliveryRequests.Count.ShouldBe(recipientCount);
        deliveryRequests.Select(item => item.UserId).Distinct().Count().ShouldBe(recipientCount);
    }

    [Fact]
    public async Task Cancellation_stops_provider_work_before_the_next_candidate_batch()
    {
        var eventBus = Substitute.For<IDistributedEventBus>();
        using var cancellation = new CancellationTokenSource();
        var publishedBatches = 0;
        eventBus.WhenForAnyArgs(bus => bus.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(_ =>
            {
                publishedBatches++;
                cancellation.Cancel();
            });
        var notificationId = Guid.NewGuid();
        var distributor = CreateDistributor(eventBus, new NotificationDistributionOptions
        {
            RecipientBatchSize = 2
        });

        await Should.ThrowAsync<OperationCanceledException>(() => WithUnitOfWorkAsync(() =>
            distributor.DistributeAsync(
                NewNotification(notificationId),
                Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray(),
                null,
                cancellation.Token)));

        publishedBatches.ShouldBe(1);
        await WithUnitOfWorkAsync(async () =>
        {
            // EF rolls the ambient transaction back, while the non-transactional Mongo test provider can retain
            // the completed first batch. Neither provider may advance beyond that cancellation boundary.
            var retainedRows = await GetRequiredService<IRepository<UserNotification, Guid>>()
                .GetListAsync(row => row.NotificationId == notificationId);
            retainedRows.Count.ShouldBeLessThanOrEqualTo(2);
        });
    }

    [Fact]
    public async Task Large_explicit_publish_enqueues_one_background_job_that_persists_and_delivers_all_users()
    {
        var options = new NotificationDistributionOptions
        {
            DirectDistributionUserThreshold = 1,
            RecipientBatchSize = 256
        };
        var eventBus = Substitute.For<IDistributedEventBus>();
        var deliveries = new List<NotificationDeliveryRequestedEto>();
        eventBus.WhenForAnyArgs(bus => bus.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(call => deliveries.Add(call.Arg<NotificationDeliveryRequestedEto>()));
        var distributor = CreateDistributor(eventBus, options);
        var backgroundJobManager = Substitute.For<IBackgroundJobManager>();
        var publisher = new DefaultNotificationPublisher(
            Options.Create(options),
            distributor,
            backgroundJobManager,
            GetRequiredService<IGuidGenerator>(),
            GetRequiredService<IClock>(),
            GetRequiredService<ICurrentTenant>(),
            GetRequiredService<INotificationDefinitionManager>());
        var distinctUsers = Enumerable.Range(0, 2_001).Select(_ => Guid.NewGuid()).ToArray();
        var users = distinctUsers
            .Concat(new[] { distinctUsers[0], distinctUsers[255], distinctUsers[256], distinctUsers[^1] })
            .ToArray();

        await WithUnitOfWorkAsync(() => publisher.PublishAsync(
            "order.shipped",
            new MessageNotificationData("bulk"),
            userIds: users));

        // A large explicit fan-out is a single background job carrying the whole list; the distributor batches
        // recipients internally when the job runs.
        var args = backgroundJobManager.ReceivedCalls()
            .SelectMany(call => call.GetArguments().OfType<NotificationDistributionJobArgs>())
            .Single();
        args.UserIds.ShouldNotBeNull();
        args.UserIds!.Length.ShouldBe(users.Length);

        await WithUnitOfWorkAsync(() => new NotificationDistributionJob(
                distributor,
                GetRequiredService<ICurrentTenant>(),
                GetRequiredService<IUnitOfWorkManager>())
            .ExecuteAsync(args));

        await WithUnitOfWorkAsync(async () =>
        {
            var notificationId = args.Notification.Id;
            (await GetRequiredService<IRepository<Notification, Guid>>()
                .FindAsync(notificationId)).ShouldNotBeNull();
            var rows = await GetRequiredService<IRepository<UserNotification, Guid>>()
                .GetListAsync(row => row.NotificationId == notificationId);
            rows.Count.ShouldBe(distinctUsers.Length);
            rows.Select(row => row.UserId).ShouldBe(distinctUsers, ignoreOrder: true);
        });
        deliveries.Count.ShouldBe(distinctUsers.Length);
        deliveries.Select(delivery => delivery.UserId).ShouldBe(distinctUsers, ignoreOrder: true);
    }
}
