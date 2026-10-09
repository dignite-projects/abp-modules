using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The receiving side of remote publishing, in a stateless Core + Distribution host: a publish request that arrives
/// through the distributed event bus is distributed by this process exactly as a local publish would be.
/// </summary>
public class NotificationPublishRequestedHandler_Tests : DigniteAbpNotificationsTestBase
{
    private readonly IDistributedEventBus _eventBus;
    private readonly ReceivedNotificationDeliveries _received;
    private readonly FakeBackgroundJobManager _backgroundJobs;
    private readonly ICurrentTenant _currentTenant;

    public NotificationPublishRequestedHandler_Tests()
    {
        _eventBus = GetRequiredService<IDistributedEventBus>();
        _received = GetRequiredService<ReceivedNotificationDeliveries>();
        _backgroundJobs = GetRequiredService<FakeBackgroundJobManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static NotificationPublishRequestedEto NewRequest(Guid[]? userIds, Guid? tenantId = null, string[]? channels = null)
    {
        return new NotificationPublishRequestedEto
        {
            NotificationId = Guid.NewGuid(),
            TenantId = tenantId,
            NotificationName = TestNotificationDefinitionProvider.Plain,
            DataJson = "{\"type\":\"Dignite.Message\",\"message\":\"hi\"}",
            Severity = NotificationSeverity.Info,
            CreationTime = DateTime.UtcNow,
            UserIds = userIds,
            Channels = channels ?? new[] { SignalRNotifier.ChannelName }
        };
    }

    [Fact]
    public async Task A_small_explicit_fan_out_is_distributed_inline()
    {
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        var request = NewRequest(new[] { u1, u1, u2 });

        await _eventBus.PublishAsync(request);

        _backgroundJobs.EnqueuedArgs.ShouldBeEmpty();
        _received.Items.Select(item => item.UserId).ShouldBe(new[] { u1, u2 }, ignoreOrder: true);
        _received.Items.ShouldAllBe(item =>
            item.NotificationId == request.NotificationId &&
            item.NotificationName == request.NotificationName &&
            item.DataJson == request.DataJson &&
            item.Channel == SignalRNotifier.ChannelName);
    }

    [Fact]
    public async Task A_large_explicit_fan_out_goes_to_one_job_of_this_process()
    {
        var users = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        var tenantId = Guid.NewGuid();
        var request = NewRequest(users, tenantId, new[] { "Push" });

        await _eventBus.PublishAsync(request);

        _received.Items.ShouldBeEmpty();
        var args = _backgroundJobs.EnqueuedArgs.ShouldHaveSingleItem().ShouldBeOfType<NotificationDistributionJobArgs>();
        args.UserIds.ShouldBe(users);
        args.Notification.Id.ShouldBe(request.NotificationId);
        args.Notification.TenantId.ShouldBe(tenantId);
        args.Notification.DataJson.ShouldBe(request.DataJson);
        args.Notification.Channels.ShouldBe(new[] { "Push" });

        await GetRequiredService<NotificationDistributionJob>().ExecuteAsync(args);

        _received.Items.Select(item => item.UserId).ShouldBe(users, ignoreOrder: true);
        _received.Items.ShouldAllBe(item => item.Channel == "Push" && item.TenantId == tenantId);
    }

    [Fact]
    public async Task A_subscription_resolved_notification_goes_to_a_job()
    {
        await _eventBus.PublishAsync(NewRequest(userIds: null));

        _backgroundJobs.EnqueuedArgs.ShouldHaveSingleItem().ShouldBeOfType<NotificationDistributionJobArgs>()
            .UserIds.ShouldBeNull();
    }

    [Fact]
    public async Task An_empty_recipient_list_is_a_no_op()
    {
        await _eventBus.PublishAsync(NewRequest(Array.Empty<Guid>()));

        _received.Items.ShouldBeEmpty();
        _backgroundJobs.EnqueuedArgs.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_tenant_comes_from_the_event_not_from_the_ambient_context(bool hostNotification)
    {
        Guid? notificationTenantId = hostNotification ? null : Guid.NewGuid();

        using (_currentTenant.Change(Guid.NewGuid(), "unrelated"))
        {
            await _eventBus.PublishAsync(NewRequest(new[] { Guid.NewGuid() }, notificationTenantId));
        }

        _received.Items.ShouldHaveSingleItem().TenantId.ShouldBe(notificationTenantId);
    }

    [Fact]
    public async Task The_publishers_channels_win_over_this_process_routing()
    {
        // This host routes every test notification to SignalR; the publisher said Push and Email.
        await _eventBus.PublishAsync(NewRequest(new[] { Guid.NewGuid() }, channels: new[] { "Push", "Email" }));

        _received.Items.Select(item => item.Channel).ShouldBe(new[] { "Push", "Email" }, ignoreOrder: true);
    }

    [Fact]
    public async Task A_payload_type_this_process_does_not_know_reaches_the_channels_unchanged()
    {
        // The notification service registers no business payload types; SignalR passes the JSON through as an object.
        const string dataJson = "{\"type\":\"Publisher.Only.Payload\",\"orderNumber\":\"SO-1\",\"lines\":[1,2]}";
        var request = NewRequest(new[] { Guid.NewGuid() });
        request.DataJson = dataJson;

        await _eventBus.PublishAsync(request);

        var delivery = _received.Items.ShouldHaveSingleItem();
        delivery.DataJson.ShouldBe(dataJson);

        var message = SignalRNotificationMessage.FromRequest(delivery);
        message.Data.ShouldNotBeNull();
        message.Data!.Value.GetRawText().ShouldBe(dataJson);
        JsonSerializer.Serialize(message, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .ShouldContain("\"data\":" + dataJson);
    }

    [Fact]
    public async Task Pre_resolved_channels_are_used_without_asking_the_resolver()
    {
        var store = Substitute.For<INotificationStore>();
        var definitionManager = Substitute.For<INotificationDefinitionManager>();
        definitionManager.IsAvailableAsync("test", Arg.Any<Guid>()).Returns(true);
        var resolver = Substitute.For<INotificationChannelResolver>();
        var eventBus = Substitute.For<IDistributedEventBus>();
        var published = new List<NotificationDeliveryRequestedEto>();
        eventBus.WhenForAnyArgs(bus => bus.PublishAsync(Arg.Any<NotificationDeliveryRequestedEto>()))
            .Do(call => published.Add(call.Arg<NotificationDeliveryRequestedEto>()));

        var distributor = new DefaultNotificationDistributor(
            store,
            definitionManager,
            resolver,
            eventBus,
            new TestCurrentTenant(),
            NullLogger<DefaultNotificationDistributor>.Instance,
            Options.Create(new NotificationDistributionOptions()));

        await distributor.DistributeAsync(
            new NotificationInfo { Id = Guid.NewGuid(), NotificationName = "test", Channels = new[] { "Email" } },
            new[] { Guid.NewGuid() });

        await resolver.Received(0).ResolveAsync(
            Arg.Any<NotificationDefinition>(), Arg.Any<NotificationInfo>(), Arg.Any<CancellationToken>());
        // Nor is the definition looked up for routing: a notification service may not know the definition yet.
        definitionManager.DidNotReceiveWithAnyArgs().Get(default!);
        published.ShouldHaveSingleItem().Channel.ShouldBe("Email");
    }
}
