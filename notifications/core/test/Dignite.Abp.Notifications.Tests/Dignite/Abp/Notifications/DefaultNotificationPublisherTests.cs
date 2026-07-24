using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.Abp.Notifications;

public class DefaultNotificationPublisherTests
{
    private readonly INotificationDistributor _distributor = Substitute.For<INotificationDistributor>();
    private readonly IBackgroundJobManager _backgroundJobManager = Substitute.For<IBackgroundJobManager>();
    private readonly INotificationDefinitionManager _definitionManager =
        Substitute.For<INotificationDefinitionManager>();

    public DefaultNotificationPublisherTests()
    {
        _definitionManager.Get(Arg.Any<string>()).Returns(call =>
            new NotificationDefinition(
                call.Arg<string>(),
                new FixedLocalizableString(call.Arg<string>())));
    }

    // These tests substitute INotificationStore, so there's no real DbContext for a UoW to protect — just enough
    // of a fake to satisfy NotificationDistributionJob's Begin(requiresNew: true)/CompleteAsync calls.
    private static IUnitOfWorkManager CreateFakeUnitOfWorkManager()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var unitOfWorkManager = Substitute.For<IUnitOfWorkManager>();
        // NotificationDistributionJob calls the Begin(bool, bool) convenience overload, but that's a static
        // extension method (UnitOfWorkManagerExtensions.Begin) that forwards to this actual interface method —
        // NSubstitute can only intercept the real member.
        unitOfWorkManager.Begin(Arg.Any<AbpUnitOfWorkOptions>(), Arg.Any<bool>()).Returns(unitOfWork);
        return unitOfWorkManager;
    }

    private DefaultNotificationPublisher CreatePublisher(int threshold, Guid? tenantId = null)
    {
        var options = Options.Create(new NotificationDistributionOptions
        {
            DirectDistributionUserThreshold = threshold
        });

        var guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        var clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTime.UtcNow);

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(tenantId);

        return new DefaultNotificationPublisher(
            options,
            _distributor,
            _backgroundJobManager,
            guidGenerator,
            clock,
            currentTenant,
            _definitionManager);
    }

    [Fact]
    public async Task Distributes_inline_when_at_or_below_threshold()
    {
        var tenantId = Guid.NewGuid();
        var publisher = CreatePublisher(threshold: 3, tenantId);
        var users = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        await publisher.PublishAsync("test", userIds: users);

        await _distributor.Received(1).DistributeAsync(
            Arg.Is<NotificationInfo>(n => n.TenantId == tenantId),
            Arg.Is<Guid[]>(u => u.Length == 3),
            Arg.Any<Guid[]?>());
        await _backgroundJobManager.DidNotReceiveWithAnyArgs()
            .EnqueueAsync(Arg.Any<NotificationDistributionJobArgs>());
    }

    [Fact]
    public async Task Empty_explicit_recipient_list_is_a_no_op()
    {
        var publisher = CreatePublisher(threshold: 3);

        await publisher.PublishAsync("test", userIds: Array.Empty<Guid>());

        await _distributor.DidNotReceiveWithAnyArgs().DistributeAsync(default!, default, default);
        await _backgroundJobManager.DidNotReceiveWithAnyArgs()
            .EnqueueAsync(Arg.Any<NotificationDistributionJobArgs>());
    }

    [Fact]
    public async Task Undefined_notification_name_fails_before_any_side_effect()
    {
        _definitionManager.Get("missing").Returns(_ => throw new AbpException("Undefined notification: missing"));
        var publisher = CreatePublisher(threshold: 3);

        await Should.ThrowAsync<AbpException>(() => publisher.PublishAsync(
            "missing",
            userIds: new[] { Guid.NewGuid() }));

        await _distributor.DidNotReceiveWithAnyArgs().DistributeAsync(default!, default, default);
        await _backgroundJobManager.DidNotReceiveWithAnyArgs()
            .EnqueueAsync(Arg.Any<NotificationDistributionJobArgs>());
    }

    [Fact]
    public async Task Duplicate_recipients_are_normalized_before_the_inline_threshold_is_evaluated()
    {
        var publisher = CreatePublisher(threshold: 2);
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();

        // 3 entries but only 2 distinct users: stays inline. The distributor owns final deduplication.
        await publisher.PublishAsync("test", userIds: new[] { u1, u1, u2 });

        await _distributor.Received(1).DistributeAsync(
            Arg.Any<NotificationInfo>(),
            Arg.Any<Guid[]?>(),
            Arg.Any<Guid[]?>());
        await _backgroundJobManager.DidNotReceiveWithAnyArgs()
            .EnqueueAsync(Arg.Any<NotificationDistributionJobArgs>());
    }

    [Fact]
    public async Task Duplicate_recipients_do_not_force_a_small_fan_out_into_the_background()
    {
        var publisher = CreatePublisher(threshold: 1);
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();

        // 2 distinct users over a threshold of 1: goes to the background job with the caller's array.
        await publisher.PublishAsync("test", userIds: new[] { u1, u1, u2, u2 });

        await _backgroundJobManager.Received(1).EnqueueAsync(
            Arg.Is<NotificationDistributionJobArgs>(args =>
                args.UserIds != null && args.UserIds.Length == 4));
        await _distributor.DidNotReceiveWithAnyArgs().DistributeAsync(default!, default, default);
    }

    [Fact]
    public async Task Publishes_the_caller_supplied_entity_type_name_verbatim()
    {
        var publisher = CreatePublisher(threshold: 3);

        await publisher.PublishAsync(
            "test",
            entityIdentifier: new NotificationEntityIdentifier("Demo.Order", "1001"),
            userIds: new[] { Guid.NewGuid() });

        // EntityTypeName is persisted, matched by string equality against stored subscriptions, returned over REST
        // and used as the EntityLinkResolvers key. It must be the caller's stable string, never a CLR type name —
        // notifications-invariants.md §1.
        await _distributor.Received(1).DistributeAsync(
            Arg.Is<NotificationInfo>(n => n.EntityTypeName == "Demo.Order" && n.EntityId == "1001"),
            Arg.Any<Guid[]?>(),
            Arg.Any<Guid[]?>());
    }

    [Fact]
    public async Task Enqueues_a_single_background_job_when_above_threshold()
    {
        var tenantId = Guid.NewGuid();
        var publisher = CreatePublisher(threshold: 2, tenantId);
        var users = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        var excluded = new[] { users[^1] };

        await publisher.PublishAsync("test", userIds: users, excludedUserIds: excluded);

        // One job carries the whole explicit fan-out; the distributor batches recipients internally.
        await _backgroundJobManager.Received(1).EnqueueAsync(
            Arg.Is<NotificationDistributionJobArgs>(args =>
                args.Notification.TenantId == tenantId
                && args.UserIds != null
                && args.UserIds.SequenceEqual(users)
                && args.ExcludedUserIds != null
                && args.ExcludedUserIds.SequenceEqual(excluded)));
        await _distributor.DidNotReceiveWithAnyArgs().DistributeAsync(default!, default, default);
    }

    [Fact]
    public async Task Null_recipient_list_is_preserved_for_subscription_distribution()
    {
        var publisher = CreatePublisher(threshold: 3);

        await publisher.PublishAsync("test", userIds: null);

        await _backgroundJobManager.Received(1).EnqueueAsync(
            Arg.Is<NotificationDistributionJobArgs>(args => args.UserIds == null));
        await _distributor.DidNotReceiveWithAnyArgs().DistributeAsync(default!, default, default);
    }

    /// <summary>
    /// The background worker is the only context that has to restore a tenant: ABP's BackgroundJobExecuter only does
    /// it for IMultiTenant job args, and NotificationDistributionJobArgs is not one. So these tests run the job over a
    /// REAL DefaultNotificationDistributor, starting from no ambient tenant, and assert on what the pipeline actually
    /// observed — substituting the distributor would only prove that Change() was called, not that it mattered.
    /// </summary>
    private sealed class JobPipeline
    {
        public TestCurrentTenant CurrentTenant { get; } = new();
        public INotificationStore Store { get; } = Substitute.For<INotificationStore>();
        public IDistributedEventBus EventBus { get; } = Substitute.For<IDistributedEventBus>();

        public bool StoreWasCalled { get; private set; }
        public Guid? TenantSeenByStore { get; private set; }
        public Guid? TenantSeenByPublish { get; private set; }
        public NotificationDeliveryRequestedEto? Published { get; private set; }

        public NotificationDistributionJob CreateJob()
        {
            var definitionManager = Substitute.For<INotificationDefinitionManager>();
            definitionManager.Get("test")
                .Returns(new NotificationDefinition("test", new FixedLocalizableString("Test")).UseChannels("Test"));
            definitionManager.IsAvailableAsync("test", Arg.Any<Guid>()).Returns(true);

            Store.When(x => x.InsertNotificationAsync(Arg.Any<NotificationInfo>()))
                .Do(_ =>
                {
                    StoreWasCalled = true;
                    TenantSeenByStore = CurrentTenant.Id;
                });
            EventBus.WhenForAnyArgs(x => x.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
                .Do(ci =>
                {
                    TenantSeenByPublish = CurrentTenant.Id;
                    Published = ci.Arg<NotificationDeliveryRequestedEto>();
                });

            return new NotificationDistributionJob(
                new DefaultNotificationDistributor(
                    Store,
                    definitionManager,
                    EventBus,
                    NotificationTestObjects.CreateSerializer(),
                    CurrentTenant,
                    NullLogger<DefaultNotificationDistributor>.Instance,
                    Options.Create(new NotificationDistributionOptions())),
                CurrentTenant,
                CreateFakeUnitOfWorkManager());
        }

        public Task ExecuteAsync(Guid? notificationTenantId)
        {
            return ExecuteAsync(notificationTenantId, new[] { Guid.NewGuid() });
        }

        public Task ExecuteAsync(Guid? notificationTenantId, Guid[]? userIds)
        {
            return CreateJob().ExecuteAsync(new NotificationDistributionJobArgs(
                new NotificationInfo
                {
                    Id = Guid.NewGuid(),
                    NotificationName = "test",
                    TenantId = notificationTenantId
                },
                userIds,
                null));
        }
    }

    [Fact]
    public async Task Distribution_job_restores_the_notification_tenant_on_the_background_worker()
    {
        var tenantId = Guid.NewGuid();
        var pipeline = new JobPipeline();

        // A worker thread picked the job off the queue: no ambient tenant.
        pipeline.CurrentTenant.Id.ShouldBeNull();

        await pipeline.ExecuteAsync(tenantId);

        pipeline.TenantSeenByStore.ShouldBe(tenantId);
        pipeline.TenantSeenByPublish.ShouldBe(tenantId);
        pipeline.Published.ShouldNotBeNull();
        pipeline.Published!.TenantId.ShouldBe(tenantId);
        pipeline.CurrentTenant.Id.ShouldBeNull();
    }

    [Fact]
    public async Task Distribution_job_stays_on_the_host_when_the_notification_has_no_tenant()
    {
        var pipeline = new JobPipeline();

        await pipeline.ExecuteAsync(notificationTenantId: null);

        pipeline.StoreWasCalled.ShouldBeTrue();
        pipeline.TenantSeenByStore.ShouldBeNull();
        pipeline.TenantSeenByPublish.ShouldBeNull();
        pipeline.Published.ShouldNotBeNull();
        pipeline.Published!.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task Distribution_job_preserves_an_empty_explicit_list_as_a_no_op()
    {
        var pipeline = new JobPipeline();

        await pipeline.ExecuteAsync(Guid.NewGuid(), Array.Empty<Guid>());

        pipeline.StoreWasCalled.ShouldBeFalse();
        pipeline.Published.ShouldBeNull();
        await pipeline.Store.DidNotReceiveWithAnyArgs().GetSubscriptionUserIdsAsync(
            default!, default, default, default, default, default);
    }

    [Fact]
    public async Task Distribution_job_forwards_cancellation_to_the_canonical_distributor()
    {
        var distributor = Substitute.For<INotificationDistributor>();
        var currentTenant = new TestCurrentTenant();
        using var cancellation = new CancellationTokenSource();
        var notification = new NotificationInfo
        {
            Id = Guid.NewGuid(),
            NotificationName = "test",
            TenantId = Guid.NewGuid()
        };
        var userIds = new[] { Guid.NewGuid() };
        var job = new NotificationDistributionJob(distributor, currentTenant, CreateFakeUnitOfWorkManager());

        await job.ExecuteWithCancellationAsync(
            new NotificationDistributionJobArgs(notification, userIds, null),
            cancellation.Token);

        await distributor.Received(1).DistributeAsync(
            notification,
            userIds,
            null,
            cancellation.Token);
        currentTenant.Id.ShouldBeNull();
    }

    [Fact]
    public void Job_type_exposes_exactly_one_public_ExecuteAsync_method()
    {
        // Volo.Abp.BackgroundJobs.BackgroundJobExecuter locates a job's execute method by name via reflection
        // (Type.GetMethod("ExecuteAsync")), which throws AmbiguousMatchException if a second public overload
        // shares that name — this is unreachable from a direct call in tests, only from the real background-job
        // worker, which is exactly how the subscription-driven publish path silently stopped delivering in
        // production. Keep any additional execute overload under a different name (e.g. ExecuteWithCancellationAsync).
        Should.NotThrow(() => typeof(NotificationDistributionJob).GetMethod("ExecuteAsync"));
    }
}
