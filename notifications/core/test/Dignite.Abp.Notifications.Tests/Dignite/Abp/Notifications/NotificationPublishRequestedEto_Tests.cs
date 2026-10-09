using System;
using System.Text;
using System.Text.Json;
using Shouldly;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The publish request crosses processes through ABP's event bus — outbox and inbox included — which serializes ETOs
/// with plain System.Text.Json and no application options. It must survive exactly that round trip (invariants §1).
/// </summary>
public class NotificationPublishRequestedEto_Tests
{
    [Fact]
    public void Eto_round_trips_through_default_stj_as_abp_event_boxes_serialize_it()
    {
        var serializer = NotificationTestObjects.CreateSerializer(typeof(OrderShippedNotificationData));
        var eto = new NotificationPublishRequestedEto
        {
            NotificationId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            NotificationName = "Test.OrderShipped",
            DataJson = serializer.Serialize(new OrderShippedNotificationData { OrderNumber = "SO-7", ItemCount = 2 }),
            Severity = NotificationSeverity.Success,
            EntityTypeName = "Demo.Order",
            EntityId = "1001",
            CreationTime = new DateTime(2026, 10, 9, 8, 30, 0, DateTimeKind.Utc),
            UserIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
            ExcludedUserIds = new[] { Guid.NewGuid() },
            Channels = new[] { "SignalR", "Email" }
        };

        var wireBytes = JsonSerializer.SerializeToUtf8Bytes(eto);
        var back = JsonSerializer.Deserialize<NotificationPublishRequestedEto>(wireBytes)!;

        back.NotificationId.ShouldBe(eto.NotificationId);
        back.TenantId.ShouldBe(eto.TenantId);
        back.NotificationName.ShouldBe(eto.NotificationName);
        back.DataJson.ShouldBe(eto.DataJson);
        back.Severity.ShouldBe(eto.Severity);
        back.EntityTypeName.ShouldBe(eto.EntityTypeName);
        back.EntityId.ShouldBe(eto.EntityId);
        back.CreationTime.ShouldBe(eto.CreationTime);
        back.UserIds.ShouldBe(eto.UserIds);
        back.ExcludedUserIds.ShouldBe(eto.ExcludedUserIds);
        back.Channels.ShouldBe(eto.Channels);

        // The payload is identified by its discriminator, never by a CLR name.
        var wireJson = Encoding.UTF8.GetString(wireBytes);
        wireJson.ShouldContain("Test.OrderShipped");
        wireJson.ShouldNotContain(nameof(OrderShippedNotificationData));
        wireJson.ShouldNotContain("Version=");
        serializer.Deserialize(back.DataJson).ShouldBeOfType<OrderShippedNotificationData>().OrderNumber.ShouldBe("SO-7");
    }

    [Fact]
    public void Host_tenant_subscription_recipients_and_inbox_only_round_trip_as_null()
    {
        var eto = new NotificationPublishRequestedEto
        {
            NotificationId = Guid.NewGuid(),
            NotificationName = "Test.Plain"
        };

        var back = JsonSerializer.Deserialize<NotificationPublishRequestedEto>(JsonSerializer.SerializeToUtf8Bytes(eto))!;

        back.TenantId.ShouldBeNull();
        back.DataJson.ShouldBeNull();
        back.UserIds.ShouldBeNull();
        back.ExcludedUserIds.ShouldBeNull();
        back.Channels.ShouldBeNull();
    }

    [Fact]
    public void Event_name_is_a_stable_contract_and_the_tenant_is_authoritative()
    {
        EventNameAttribute.GetNameOrDefault(typeof(NotificationPublishRequestedEto))
            .ShouldBe("Dignite.Abp.Notifications.NotificationPublishRequested");

        // IMultiTenant, not IEventDataMayHaveTenantId: ABP's event bus runs the handler in TenantId even when it is null
        // (host), instead of falling back to the ambient tenant.
        typeof(IMultiTenant).IsAssignableFrom(typeof(NotificationPublishRequestedEto)).ShouldBeTrue();
        typeof(IEventDataMayHaveTenantId).IsAssignableFrom(typeof(NotificationPublishRequestedEto)).ShouldBeFalse();
    }
}
