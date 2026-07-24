using System;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Dignite.Abp.Notifications;

public class NotificationDataSerializationTests
{
    [Fact]
    public void Custom_data_round_trips_across_independent_registries()
    {
        var server = NotificationTestObjects.CreateSerializer(typeof(OrderShippedNotificationData));
        var client = NotificationTestObjects.CreateSerializer(typeof(OrderShippedNotificationData));

        var json = server.Serialize(new OrderShippedNotificationData
        {
            OrderNumber = "SO-1001",
            ItemCount = 3
        });

        json.ShouldNotBeNull();
        var back = client.Deserialize(json).ShouldBeOfType<OrderShippedNotificationData>();
        back.OrderNumber.ShouldBe("SO-1001");
        back.ItemCount.ShouldBe(3);
    }

    [Fact]
    public void New_writes_use_the_stable_discriminator_and_no_clr_name()
    {
        var serializer = NotificationTestObjects.CreateSerializer(typeof(OrderShippedNotificationData));

        var json = serializer.Serialize(new OrderShippedNotificationData
        {
            OrderNumber = "SO-1",
            ItemCount = 1
        })!;

        json.ShouldContain("\"type\":\"Test.OrderShipped\"");
        json.ShouldNotContain("Version=");
        json.ShouldNotContain("OrderShippedNotificationData");
    }

    [Fact]
    public void Unknown_discriminator_becomes_an_unsupported_placeholder()
    {
        const string json = "{\"type\":\"Vendor.Removed\",\"secret\":\"x\"}";
        var serializer = NotificationTestObjects.CreateSerializer();

        var unsupported = serializer.Deserialize(json).ShouldBeOfType<UnsupportedNotificationData>();
        unsupported.Reason.ShouldBe(UnsupportedNotificationDataReason.UnknownDiscriminator);
        unsupported.OriginalDiscriminator.ShouldBe("Vendor.Removed");
        unsupported.RawJson.ShouldBe(json);
    }

    [Fact]
    public void Malformed_known_payload_is_distinguishable_and_preserves_raw_json()
    {
        const string json =
            "{\"type\":\"Test.OrderShipped\",\"orderNumber\":\"SO-1\",\"itemCount\":\"bad\"}";
        var serializer = NotificationTestObjects.CreateSerializer(typeof(OrderShippedNotificationData));

        var unsupported = serializer.Deserialize(json).ShouldBeOfType<UnsupportedNotificationData>();
        unsupported.Reason.ShouldBe(UnsupportedNotificationDataReason.MalformedPayload);
        unsupported.OriginalDiscriminator.ShouldBe("Test.OrderShipped");
        unsupported.RawJson.ShouldBe(json);
    }

    [Fact]
    public void Ordinary_model_materialization_exceptions_are_tolerated_without_failing_a_batch()
    {
        const string json = "{\"type\":\"Test.ThrowingSetter\",\"value\":\"bad\"}";
        var serializer = NotificationTestObjects.CreateSerializer(typeof(ThrowingSetterNotificationData));

        var unsupported = serializer.Deserialize(json).ShouldBeOfType<UnsupportedNotificationData>();
        unsupported.Reason.ShouldBe(UnsupportedNotificationDataReason.MalformedPayload);
        unsupported.OriginalDiscriminator.ShouldBe("Test.ThrowingSetter");
        unsupported.RawJson.ShouldBe(json);
    }

    [Fact]
    public void Unsupported_placeholder_round_trips_as_a_known_safe_type_without_activating_original_type()
    {
        const string original = "{\"type\":\"Unknown.ArbitraryClrLookingName\",\"value\":42}";
        var serializer = NotificationTestObjects.CreateSerializer();
        var unsupported = serializer.Deserialize(original).ShouldBeOfType<UnsupportedNotificationData>();

        var json = serializer.Serialize(unsupported)!;
        var roundTrip = serializer.Deserialize(json).ShouldBeOfType<UnsupportedNotificationData>();

        json.ShouldContain("\"type\":\"Dignite.Unsupported\"");
        json.ShouldContain("\"originalDiscriminator\":\"Unknown.ArbitraryClrLookingName\"");
        json.ShouldContain("\"rawJson\":");
        roundTrip.OriginalDiscriminator.ShouldBe("Unknown.ArbitraryClrLookingName");
        roundTrip.RawJson.ShouldBe(original);
    }

    [Fact]
    public void Unknown_properties_are_preserved_as_extension_data()
    {
        var serializer = NotificationTestObjects.CreateSerializer(typeof(OrderShippedNotificationData));
        const string json =
            "{\"type\":\"Test.OrderShipped\",\"orderNumber\":\"SO-9\",\"itemCount\":1,\"trackingUrl\":\"http://x\"}";

        var data = serializer.Deserialize(json).ShouldBeOfType<OrderShippedNotificationData>();

        data.ExtensionData.ShouldNotBeNull();
        data.ExtensionData!.ShouldContainKey("trackingUrl");
    }

    [Fact]
    public void Builtin_message_data_round_trips()
    {
        var serializer = NotificationTestObjects.CreateSerializer();

        var json = serializer.Serialize(new MessageNotificationData("hello"))!;

        json.ShouldContain("\"type\":\"Dignite.Message\"");
        serializer.Deserialize(json).ShouldBeOfType<MessageNotificationData>().Message.ShouldBe("hello");
    }

    [Fact]
    public void Eto_round_trips_through_default_stj_as_abp_event_boxes_serialize_it()
    {
        var serializer = NotificationTestObjects.CreateSerializer(typeof(OrderShippedNotificationData));
        var eto = NewEvent(serializer.Serialize(new OrderShippedNotificationData
        {
            OrderNumber = "SO-7",
            ItemCount = 2
        }));

        // ABP's event bus and its outbox/inbox serialize ETOs with plain System.Text.Json and no options
        // (LocalDistributedEventBus.Serialize / PublishFromOutboxAsync) — the contract must survive exactly
        // this round trip, with no converter registered anywhere.
        var wireBytes = JsonSerializer.SerializeToUtf8Bytes(eto);
        var back = JsonSerializer.Deserialize<NotificationDeliveryRequestedEto>(wireBytes)!;

        var data = serializer.Deserialize(back.DataJson).ShouldBeOfType<OrderShippedNotificationData>();
        data.OrderNumber.ShouldBe("SO-7");
        data.ItemCount.ShouldBe(2);
    }

    [Fact]
    public void Unknown_producer_type_becomes_unsupported_on_the_consumer()
    {
        var producer = NotificationTestObjects.CreateSerializer(typeof(OrderShippedNotificationData));
        var consumer = NotificationTestObjects.CreateSerializer();
        var wireBytes = JsonSerializer.SerializeToUtf8Bytes(
            NewEvent(producer.Serialize(new OrderShippedNotificationData { OrderNumber = "SO-7", ItemCount = 2 })));

        var received = JsonSerializer.Deserialize<NotificationDeliveryRequestedEto>(wireBytes)!;

        var unsupported = consumer.Deserialize(received.DataJson).ShouldBeOfType<UnsupportedNotificationData>();
        unsupported.Reason.ShouldBe(UnsupportedNotificationDataReason.UnknownDiscriminator);
        unsupported.OriginalDiscriminator.ShouldBe("Test.OrderShipped");
    }

    [Fact]
    public void Missing_discriminator_becomes_an_unsupported_placeholder()
    {
        const string json = "{\"message\":\"x\"}";
        var serializer = NotificationTestObjects.CreateSerializer();

        serializer.Deserialize(json).ShouldBeOfType<UnsupportedNotificationData>()
            .Reason.ShouldBe(UnsupportedNotificationDataReason.MalformedPayload);
    }

    [Fact]
    public void Null_and_empty_are_handled()
    {
        var serializer = NotificationTestObjects.CreateSerializer();

        serializer.Serialize(null).ShouldBeNull();
        serializer.Deserialize(null).ShouldBeNull();
        serializer.Deserialize(string.Empty).ShouldBeNull();
    }

    private static NotificationDeliveryRequestedEto NewEvent(string? dataJson)
    {
        var notificationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string channel = "test";
        return new NotificationDeliveryRequestedEto
        {
            NotificationId = notificationId,
            NotificationName = "test",
            DataJson = dataJson,
            Severity = NotificationSeverity.Info,
            CreationTime = DateTime.UtcNow,
            UserId = userId,
            Channel = channel
        };
    }
}
