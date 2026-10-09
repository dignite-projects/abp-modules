using System;
using System.Text.Json;
using Shouldly;
using Volo.Abp.Json;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The job args carry the notification to whichever background-job backend the host uses: ABP's default job store
/// serializes them with the application's <see cref="IJsonSerializer"/>, a broker-backed job queue may use plain
/// System.Text.Json. The payload is a pre-serialized string, so both must hand it back unchanged.
/// </summary>
public class NotificationDistributionJobArgs_Tests : DigniteAbpNotificationsTestBase
{
    private const string DataJson = "{\"type\":\"Publisher.Only.Payload\",\"orderNumber\":\"SO-1\"}";

    [Fact]
    public void Job_args_round_trip_through_plain_System_Text_Json()
    {
        var args = CreateArgs();

        var back = JsonSerializer.Deserialize<NotificationDistributionJobArgs>(JsonSerializer.SerializeToUtf8Bytes(args))!;

        ShouldMatch(back, args);
    }

    [Fact]
    public void Job_args_round_trip_through_the_application_json_serializer()
    {
        var serializer = GetRequiredService<IJsonSerializer>();
        var args = CreateArgs();

        var back = serializer.Deserialize<NotificationDistributionJobArgs>(serializer.Serialize(args));

        ShouldMatch(back, args);
    }

    private static NotificationDistributionJobArgs CreateArgs()
    {
        return new NotificationDistributionJobArgs(
            new NotificationInfo
            {
                Id = Guid.NewGuid(),
                NotificationName = TestNotificationDefinitionProvider.Plain,
                DataJson = DataJson,
                EntityTypeName = "Demo.Order",
                EntityId = "1001",
                Severity = NotificationSeverity.Warn,
                CreationTime = new DateTime(2026, 10, 9, 8, 30, 0, DateTimeKind.Utc),
                TenantId = Guid.NewGuid()
            },
            new[] { Guid.NewGuid(), Guid.NewGuid() },
            new[] { Guid.NewGuid() });
    }

    private static void ShouldMatch(NotificationDistributionJobArgs actual, NotificationDistributionJobArgs expected)
    {
        actual.Notification.Id.ShouldBe(expected.Notification.Id);
        actual.Notification.NotificationName.ShouldBe(expected.Notification.NotificationName);
        actual.Notification.DataJson.ShouldBe(DataJson);
        actual.Notification.EntityTypeName.ShouldBe(expected.Notification.EntityTypeName);
        actual.Notification.EntityId.ShouldBe(expected.Notification.EntityId);
        actual.Notification.Severity.ShouldBe(expected.Notification.Severity);
        actual.Notification.CreationTime.ShouldBe(expected.Notification.CreationTime);
        actual.Notification.TenantId.ShouldBe(expected.Notification.TenantId);
        actual.UserIds.ShouldBe(expected.UserIds);
        actual.ExcludedUserIds.ShouldBe(expected.ExcludedUserIds);
    }
}
