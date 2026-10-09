using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Remote;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Testing;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The remote publisher decides only what the publishing process knows — the definition exists, the payload JSON, the
/// channels — and sends exactly one event per notification.
/// </summary>
public class RemoteNotificationPublisher_Tests
{
    private readonly INotificationDefinitionManager _definitionManager = Substitute.For<INotificationDefinitionManager>();
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly List<NotificationPublishRequestedEto> _published = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 10, 9, 8, 30, 0, DateTimeKind.Utc);

    public RemoteNotificationPublisher_Tests()
    {
        _definitionManager.Get("test").Returns(new NotificationDefinition("Test", "test", new FixedLocalizableString("Test")));
        _definitionManager.Get("missing").Returns(_ => throw new AbpException("Undefined notification: missing"));
        _eventBus.WhenForAnyArgs(bus => bus.PublishAsync(Arg.Any<NotificationPublishRequestedEto>()))
            .Do(call => _published.Add(call.Arg<NotificationPublishRequestedEto>()));
    }

    private RemoteNotificationPublisher CreatePublisher(INotificationChannelResolver? resolver = null)
    {
        var guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());
        var clock = Substitute.For<IClock>();
        clock.Now.Returns(_now);
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(_tenantId);

        return new RemoteNotificationPublisher(
            _definitionManager,
            resolver ?? NotificationTestObjects.CreateChannelResolver("SignalR"),
            NotificationTestObjects.CreateSerializer(),
            _eventBus,
            guidGenerator,
            clock,
            currentTenant);
    }

    [Fact]
    public async Task An_undefined_notification_throws_and_sends_nothing()
    {
        await Should.ThrowAsync<AbpException>(() => CreatePublisher().PublishAsync(
            "missing",
            userIds: new[] { Guid.NewGuid() }));

        await _eventBus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<NotificationPublishRequestedEto>());
    }

    [Fact]
    public async Task Channels_are_resolved_by_the_local_resolver_and_written_into_the_event()
    {
        var resolver = Substitute.For<INotificationChannelResolver>();
        resolver.ResolveAsync(Arg.Any<NotificationDefinition>(), Arg.Any<NotificationInfo>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "SignalR", " Email ", "email" });

        await CreatePublisher(resolver).PublishAsync("test", userIds: new[] { Guid.NewGuid() });

        await resolver.Received(1).ResolveAsync(
            Arg.Is<NotificationDefinition>(definition => definition.Name == "test"),
            Arg.Is<NotificationInfo>(notification => notification.NotificationName == "test" && notification.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
        // Normalized as the distributor would: trimmed and de-duplicated ignoring case.
        _published.ShouldHaveSingleItem().Channels.ShouldBe(new[] { "SignalR", "Email" });
    }

    [Fact]
    public async Task An_inbox_only_notification_is_sent_without_channels()
    {
        await CreatePublisher(NotificationTestObjects.CreateChannelResolver())
            .PublishAsync("test", userIds: new[] { Guid.NewGuid() });

        _published.ShouldHaveSingleItem().Channels.ShouldBeNull();
    }

    [Fact]
    public async Task The_event_carries_the_serialized_payload_the_tenant_and_the_entity()
    {
        var excluded = new[] { Guid.NewGuid() };

        await CreatePublisher().PublishAsync(
            "test",
            new MessageNotificationData("hi"),
            new NotificationEntityIdentifier("Demo.Order", "1001"),
            NotificationSeverity.Warn,
            userIds: new[] { Guid.NewGuid() },
            excludedUserIds: excluded);

        var eto = _published.ShouldHaveSingleItem();
        eto.NotificationId.ShouldNotBe(Guid.Empty);
        eto.NotificationName.ShouldBe("test");
        eto.DataJson.ShouldBe("{\"type\":\"Dignite.Message\",\"message\":\"hi\"}");
        eto.TenantId.ShouldBe(_tenantId);
        eto.EntityTypeName.ShouldBe("Demo.Order");
        eto.EntityId.ShouldBe("1001");
        eto.Severity.ShouldBe(NotificationSeverity.Warn);
        eto.CreationTime.ShouldBe(_now);
        eto.ExcludedUserIds.ShouldBe(excluded);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(1_000)]
    public async Task One_notification_is_exactly_one_event_whatever_the_recipient_count(int recipientCount)
    {
        var users = Enumerable.Range(0, recipientCount).Select(_ => Guid.NewGuid()).ToArray();
        var withDuplicates = users.Concat(users.Take(1)).ToArray();

        await CreatePublisher().PublishAsync("test", userIds: withDuplicates);

        // No deduplication and no threshold here: the receiver decides between inline distribution and a job.
        await _eventBus.ReceivedWithAnyArgs(1).PublishAsync(Arg.Any<NotificationPublishRequestedEto>());
        _published.ShouldHaveSingleItem().UserIds.ShouldBe(withDuplicates);
    }

    [Fact]
    public async Task Subscription_resolved_notification_is_one_event_without_recipients()
    {
        await CreatePublisher().PublishAsync("test", userIds: null);

        _published.ShouldHaveSingleItem().UserIds.ShouldBeNull();
    }

    [Fact]
    public async Task An_empty_recipient_list_sends_nothing()
    {
        await CreatePublisher().PublishAsync("test", userIds: Array.Empty<Guid>());

        await _eventBus.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<NotificationPublishRequestedEto>());
        _definitionManager.DidNotReceiveWithAnyArgs().Get(default!);
    }
}

/// <summary>
/// The remote publisher in a real ABP application with an outbox: inside a unit of work the event is written to the
/// publisher's outbox (in the business transaction) and not put on the bus; without one it is sent directly.
/// </summary>
public class RemoteNotificationPublisherOutbox_Tests : AbpIntegratedTest<RemoteNotificationPublisherOutbox_Tests.RemotePublisherTestModule>
{
    private const string NotificationName = "Remote.DocumentReady";

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    [Fact]
    public async Task Inside_a_unit_of_work_the_event_goes_into_the_outbox_and_not_onto_the_bus()
    {
        var publisher = GetRequiredService<INotificationPublisher>();
        publisher.ShouldBeOfType<RemoteNotificationPublisher>();

        using (var unitOfWork = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
        {
            await publisher.PublishAsync(NotificationName, new MessageNotificationData("ready"), userIds: new[] { Guid.NewGuid() });
            await unitOfWork.CompleteAsync();
        }

        GetRequiredService<ReceivedPublishRequests>().Items.ShouldBeEmpty();
        var outgoing = GetRequiredService<RecordingEventOutbox>().Events.ShouldHaveSingleItem();
        outgoing.EventName.ShouldBe("Dignite.Abp.Notifications.NotificationPublishRequested");

        // These bytes are what the receiving process reads back, with plain System.Text.Json.
        var eto = JsonSerializer.Deserialize<NotificationPublishRequestedEto>(outgoing.EventData)!;
        eto.NotificationName.ShouldBe(NotificationName);
        eto.Channels.ShouldBe(new[] { "SignalR" });
        eto.DataJson.ShouldBe("{\"type\":\"Dignite.Message\",\"message\":\"ready\"}");
    }

    [Fact]
    public async Task Without_a_unit_of_work_the_event_is_sent_directly()
    {
        await GetRequiredService<INotificationPublisher>()
            .PublishAsync(NotificationName, new MessageNotificationData("ready"), userIds: new[] { Guid.NewGuid() });

        GetRequiredService<RecordingEventOutbox>().Events.ShouldBeEmpty();
        GetRequiredService<ReceivedPublishRequests>().Items.ShouldHaveSingleItem().NotificationName.ShouldBe(NotificationName);
    }

    /// <summary>
    /// A publisher service: Core + Remote with a business module's definition and routing, an outbox, and a local
    /// consumer of the publish request that only exists to observe a direct send. The test assembly is not
    /// conventionally registered, so nothing else from it leaks in.
    /// </summary>
    [DependsOn(
        typeof(AbpNotificationsRemoteModule),
        typeof(AbpAutofacModule),
        typeof(AbpTestBaseModule))]
    public class RemotePublisherTestModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            SkipAutoServiceRegistration = true;
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpBackgroundWorkerOptions>(options => options.IsEnabled = false);

            context.Services.AddTransient<RemoteDefinitionProvider>();
            Configure<NotificationRoutingOptions>(options => options.ForNotification(NotificationName, "SignalR"));

            context.Services.AddSingleton<RecordingEventOutbox>();
            context.Services.AddSingleton<ReceivedPublishRequests>();
            context.Services.AddTransient<RecordingPublishRequestedHandler>();
            Configure<AbpDistributedEventBusOptions>(options =>
            {
                options.Outboxes.Configure("Remote", outbox =>
                {
                    outbox.DatabaseName = "Remote";
                    outbox.ImplementationType = typeof(RecordingEventOutbox);
                    outbox.IsSendingEnabled = false;
                });
                options.Handlers.Add<RecordingPublishRequestedHandler>();
            });
        }
    }

    // Registered by RemotePublisherTestModule only; the other test hosts scan this assembly and must not see it.
    [DisableConventionalRegistration]
    public class RemoteDefinitionProvider : NotificationDefinitionProvider
    {
        public override void Define(INotificationDefinitionContext context)
        {
            context.AddGroup("Remote").AddNotification(NotificationName, new FixedLocalizableString("Document ready"));
        }
    }

    public class ReceivedPublishRequests
    {
        public ConcurrentQueue<NotificationPublishRequestedEto> Items { get; } = new();
    }

    public class RecordingPublishRequestedHandler : IDistributedEventHandler<NotificationPublishRequestedEto>
    {
        private readonly ReceivedPublishRequests _received;

        public RecordingPublishRequestedHandler(ReceivedPublishRequests received)
        {
            _received = received;
        }

        public Task HandleEventAsync(NotificationPublishRequestedEto eventData)
        {
            _received.Items.Enqueue(eventData);
            return Task.CompletedTask;
        }
    }

    public class RecordingEventOutbox : IEventOutbox
    {
        public ConcurrentQueue<OutgoingEventInfo> Events { get; } = new();

        public Task EnqueueAsync(OutgoingEventInfo outgoingEvent)
        {
            Events.Enqueue(outgoingEvent);
            return Task.CompletedTask;
        }

        public Task<List<OutgoingEventInfo>> GetWaitingEventsAsync(
            int maxCount,
            Expression<Func<IOutgoingEventInfo, bool>>? filter = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Events.Take(maxCount).ToList());
        }

        public Task DeleteAsync(Guid id) => Task.CompletedTask;

        public Task DeleteManyAsync(IEnumerable<Guid> ids) => Task.CompletedTask;
    }
}
