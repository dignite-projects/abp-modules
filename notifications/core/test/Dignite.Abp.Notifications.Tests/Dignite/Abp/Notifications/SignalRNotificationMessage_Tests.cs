using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using Dignite.Abp.Notifications.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Dignite.Abp.Notifications;

public class SignalRNotificationMessage_Tests
{
    private readonly ITestOutputHelper _output;

    public SignalRNotificationMessage_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static NotificationDeliveryRequestedEto CreateRequest(string? dataJson)
    {
        return new NotificationDeliveryRequestedEto
        {
            NotificationId = Guid.NewGuid(),
            NotificationName = "test",
            DataJson = dataJson,
            Severity = NotificationSeverity.Warn,
            CreationTime = new DateTime(2026, 10, 7, 8, 30, 0, DateTimeKind.Utc),
            UserId = Guid.NewGuid(),
            Channel = SignalRNotifier.ChannelName,
            EntityTypeName = "Demo.Order",
            EntityId = "42"
        };
    }

    [Fact]
    public void Message_survives_the_real_json_hub_protocol_with_its_data_intact()
    {
        var serializer = NotificationTestObjects.CreateSerializer();
        var request = CreateRequest(serializer.Serialize(new MessageNotificationData("hi")));
        var message = SignalRNotificationMessage.FromRequest(request);

        // The hub protocol serializes with its own default System.Text.Json options — not this module's converter.
        var protocol = new JsonHubProtocol();
        var writer = new ArrayBufferWriter<byte>();
        protocol.WriteMessage(
            new InvocationMessage(nameof(INotificationsClient.ReceiveNotification), new object[] { message }),
            writer);
        var wire = Encoding.UTF8.GetString(writer.WrittenSpan).TrimEnd('\u001e');

        using var document = JsonDocument.Parse(wire);
        var argument = document.RootElement.GetProperty("arguments")[0];
        var data = argument.GetProperty("data");
        data.GetProperty("type").GetString().ShouldBe("Dignite.Message");
        data.GetProperty("message").GetString().ShouldBe("hi");
        argument.GetProperty("notificationId").GetGuid().ShouldBe(request.NotificationId);
        argument.GetProperty("notificationName").GetString().ShouldBe("test");
        argument.TryGetProperty("severity", out _).ShouldBeTrue();
        argument.GetProperty("entityTypeName").GetString().ShouldBe("Demo.Order");
        argument.GetProperty("entityId").GetString().ShouldBe("42");

        _output.WriteLine(wire);
    }

    [Fact]
    public void Plain_default_json_round_trip_preserves_every_field()
    {
        var serializer = NotificationTestObjects.CreateSerializer();
        var original = SignalRNotificationMessage.FromRequest(
            CreateRequest(serializer.Serialize(new MessageNotificationData("hi"))));

        var json = JsonSerializer.Serialize(original);
        var back = JsonSerializer.Deserialize<SignalRNotificationMessage>(json)!;

        back.NotificationId.ShouldBe(original.NotificationId);
        back.NotificationName.ShouldBe(original.NotificationName);
        back.Severity.ShouldBe(original.Severity);
        back.CreationTime.ShouldBe(original.CreationTime);
        back.EntityTypeName.ShouldBe("Demo.Order");
        back.EntityId.ShouldBe("42");
        back.Data.ShouldNotBeNull();
        back.Data!.Value.GetProperty("type").GetString().ShouldBe("Dignite.Message");
        back.Data!.Value.GetProperty("message").GetString().ShouldBe("hi");
    }

    [Fact]
    public void Null_or_blank_data_json_yields_null_data()
    {
        SignalRNotificationMessage.FromRequest(CreateRequest(null)).Data.ShouldBeNull();
        SignalRNotificationMessage.FromRequest(CreateRequest("  ")).Data.ShouldBeNull();
    }

    [Fact]
    public void Malformed_data_json_yields_null_data_and_keeps_the_envelope()
    {
        var request = CreateRequest("{not json");

        var message = SignalRNotificationMessage.FromRequest(request);

        message.Data.ShouldBeNull();
        message.NotificationId.ShouldBe(request.NotificationId);
        message.NotificationName.ShouldBe("test");
        message.Severity.ShouldBe(NotificationSeverity.Warn);
        message.CreationTime.ShouldBe(request.CreationTime);
        message.EntityTypeName.ShouldBe("Demo.Order");
        message.EntityId.ShouldBe("42");
    }

    [Fact]
    public void Unknown_discriminator_passes_through_untouched()
    {
        const string json = "{\"type\":\"Vendor.Removed\",\"secret\":\"x\"}";

        var message = SignalRNotificationMessage.FromRequest(CreateRequest(json));

        message.Data.ShouldNotBeNull();
        message.Data!.Value.GetRawText().ShouldBe(json);
    }
}
