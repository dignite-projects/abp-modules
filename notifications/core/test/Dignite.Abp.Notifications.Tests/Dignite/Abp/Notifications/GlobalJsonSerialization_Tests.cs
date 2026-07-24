using Shouldly;
using Volo.Abp.Json;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Verifies the converter is wired into ABP's global System.Text.Json pipeline, so NotificationData nested inside
/// ETOs / DTOs (event bus, HTTP API) also serializes with the stable discriminator — not just via
/// <see cref="INotificationDataSerializer"/>.
/// </summary>
public class GlobalJsonSerialization_Tests : DigniteAbpNotificationsTestBase
{
    private readonly IJsonSerializer _jsonSerializer;

    public GlobalJsonSerialization_Tests()
    {
        _jsonSerializer = GetRequiredService<IJsonSerializer>();
    }

    [Fact]
    public void Abp_json_serializer_round_trips_notification_data_with_the_discriminator()
    {
        var json = _jsonSerializer.Serialize(new MessageNotificationData("hi"));

        json.ShouldContain("Dignite.Message");   // stable discriminator, not a CLR type name
        json.ShouldNotContain("Version=");

        var back = _jsonSerializer.Deserialize<NotificationData>(json);
        back.ShouldBeOfType<MessageNotificationData>().Message.ShouldBe("hi");
    }
}
