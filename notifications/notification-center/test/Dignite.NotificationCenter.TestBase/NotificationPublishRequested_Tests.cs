using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>
/// A notification service receiving notifications published in other processes, against both persistence providers:
/// a request handled twice leaves one notification, and a payload type the service has not registered is stored and
/// served as the publisher wrote it.
/// </summary>
public abstract class NotificationPublishRequested_Tests<TStartupModule> : NotificationCenterTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static NotificationPublishRequestedEto NewRequest(string? dataJson, params Guid[] userIds)
    {
        return new NotificationPublishRequestedEto
        {
            NotificationId = Guid.NewGuid(),
            NotificationName = TestNotificationDefinitionProvider.OrderShipped,
            DataJson = dataJson,
            Severity = NotificationSeverity.Info,
            CreationTime = DateTime.UtcNow,
            UserIds = userIds,
            Channels = new[] { TestNotificationDefinitionProvider.TestChannel }
        };
    }

    [Fact]
    public async Task Handling_the_same_request_twice_stores_one_notification_and_one_inbox_row_per_user()
    {
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        var request = NewRequest(SerializeData(new MessageNotificationData("hi")), u1, u2);
        var handler = GetRequiredService<IDistributedEventHandler<NotificationPublishRequestedEto>>();

        // A redelivery the event inbox did not catch: the handler opens its own unit of work each time.
        await handler.HandleEventAsync(request);
        await handler.HandleEventAsync(request);

        await WithUnitOfWorkAsync(async () =>
        {
            (await GetRequiredService<IRepository<Notification, Guid>>()
                .GetListAsync(row => row.Id == request.NotificationId)).Count.ShouldBe(1);
            var inboxRows = await GetRequiredService<IRepository<UserNotification, Guid>>()
                .GetListAsync(row => row.NotificationId == request.NotificationId);
            inboxRows.Select(row => row.UserId).ShouldBe(new[] { u1, u2 }, ignoreOrder: true);
        });
    }

    [Fact]
    public async Task An_unregistered_payload_type_is_stored_and_served_by_the_inbox_as_published()
    {
        // The publisher's business module registered this discriminator; the notification service did not.
        const string dataJson = "{\"type\":\"Publisher.Only.Payload\",\"orderNumber\":\"SO-1\",\"lines\":[1,2]}";
        var userId = Guid.NewGuid();
        var request = NewRequest(dataJson, userId);

        await GetRequiredService<IDistributedEventHandler<NotificationPublishRequestedEto>>().HandleEventAsync(request);

        await WithUnitOfWorkAsync(async () =>
        {
            (await GetRequiredService<IRepository<Notification, Guid>>().GetAsync(request.NotificationId))
                .Data.ShouldBe(dataJson);
        });

        using (ChangeCurrentUser(userId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var dto = (await GetRequiredService<IUserNotificationAppService>()
                        .GetListAsync(new GetUserNotificationListInput()))
                    .Items.ShouldHaveSingleItem();

                dto.NotificationId.ShouldBe(request.NotificationId);
                // The tolerant read: the inbox cannot type it, so it keeps the original JSON byte for byte.
                var unsupported = dto.Data.ShouldBeOfType<UnsupportedNotificationData>();
                unsupported.Reason.ShouldBe(UnsupportedNotificationDataReason.UnknownDiscriminator);
                unsupported.OriginalDiscriminator.ShouldBe("Publisher.Only.Payload");
                unsupported.RawJson.ShouldBe(dataJson);

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                options.Converters.Add(new NotificationDataJsonConverter(
                    GetRequiredService<INotificationDataTypeRegistry>()));
                var restJson = JsonSerializer.Serialize(dto, options);
                restJson.ShouldContain("\"originalDiscriminator\":\"Publisher.Only.Payload\"");
                restJson.ShouldContain("\"rawJson\":" + JsonSerializer.Serialize(dataJson));
            });
        }
    }
}
