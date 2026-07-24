using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>
/// Provider-agnostic inbox (<see cref="IUserNotificationAppService"/>) and subscription
/// (<see cref="INotificationSubscriptionAppService"/>) scenarios, run against both the EF Core and MongoDB
/// providers via the thin subclasses in each provider test project.
/// </summary>
public abstract class UserNotificationAppService_Tests<TStartupModule> : NotificationCenterTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly Guid _userId = Guid.NewGuid();

    private async Task<Guid> SeedNotificationAsync(
        NotificationData data, UserNotificationState state = UserNotificationState.Unread)
    {
        var notificationId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            await store.InsertNotificationAsync(new NotificationInfo
            {
                Id = notificationId,
                NotificationName = "order.shipped",
                Data = data,
                Severity = NotificationSeverity.Info,
                CreationTime = DateTime.UtcNow
            });
            await store.InsertUserNotificationAsync(new UserNotificationInfo
            {
                UserId = _userId,
                NotificationId = notificationId,
                State = state,
                CreationTime = DateTime.UtcNow
            });
        });
        return notificationId;
    }

    [Fact]
    public async Task GetList_returns_typed_data_and_a_read_time_localized_display_name()
    {
        await SeedNotificationAsync(new OrderShippedNotificationData { OrderNumber = "SO-1", ItemCount = 2 });

        using (ChangeCurrentUser(_userId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var appService = GetRequiredService<IUserNotificationAppService>();
                var result = await appService.GetListAsync(new GetUserNotificationListInput());

                result.TotalCount.ShouldBe(1);
                var dto = result.Items.Single();
                dto.NotificationName.ShouldBe("order.shipped");
                dto.NotificationDisplayName.ShouldBe("Order Shipped");
                dto.Data.ShouldBeOfType<OrderShippedNotificationData>().OrderNumber.ShouldBe("SO-1");
                dto.State.ShouldBe(UserNotificationState.Unread);
            });
        }
    }

    [Fact]
    public async Task GetList_returns_an_unsupported_placeholder_and_serializable_rest_fallback_metadata()
    {
        var notificationId = Guid.NewGuid();
        var creationTime = DateTime.UtcNow;
        var rawJson = HistoricalPayloadFixtures.Read("unknown-payload-v1.json");
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<Notification, Guid>>()
                .InsertAsync(new Notification(
                    notificationId,
                    "order.shipped",
                    rawJson,
                    null,
                    null,
                    NotificationSeverity.Info,
                    creationTime,
                    null));
            await GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<UserNotification, Guid>>()
                .InsertAsync(new UserNotification(
                    Guid.NewGuid(),
                    _userId,
                    notificationId,
                    UserNotificationState.Unread,
                    creationTime,
                    null));
        });

        using (ChangeCurrentUser(_userId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var result = await GetRequiredService<IUserNotificationAppService>()
                    .GetListAsync(new GetUserNotificationListInput());
                var dto = result.Items.Single(item => item.NotificationId == notificationId);
                var unsupported = dto.Data.ShouldBeOfType<UnsupportedNotificationData>();
                unsupported.Reason.ShouldBe(UnsupportedNotificationDataReason.UnknownDiscriminator);
                unsupported.RawJson.ShouldBe(rawJson);

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                options.Converters.Add(new NotificationDataJsonConverter(
                    GetRequiredService<INotificationDataTypeRegistry>()));
                var restJson = JsonSerializer.Serialize(dto, options);
                restJson.ShouldContain("\"type\":\"Dignite.Unsupported\"");
                restJson.ShouldContain("\"originalDiscriminator\":\"Removed.Module.Payload\"");
                restJson.ShouldContain("\"reason\":\"UnknownDiscriminator\"");
            });
        }
    }

    [Fact]
    public async Task Marking_as_read_updates_state_and_count()
    {
        var notificationId = await SeedNotificationAsync(new MessageNotificationData("hi"));

        using (ChangeCurrentUser(_userId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var appService = GetRequiredService<IUserNotificationAppService>();
                (await appService.GetUnreadCountAsync()).ShouldBe(1);
                await appService.MarkAsReadAsync(notificationId);
            });

            await WithUnitOfWorkAsync(async () =>
            {
                var appService = GetRequiredService<IUserNotificationAppService>();
                (await appService.GetUnreadCountAsync()).ShouldBe(0);
                (await appService.GetListAsync(new GetUserNotificationListInput { State = UserNotificationState.Read }))
                    .TotalCount.ShouldBe(1);
            });
        }
    }

    [Fact]
    public async Task Deleting_all_read_removes_read_notifications_and_preserves_unread()
    {
        await SeedNotificationAsync(new MessageNotificationData("read me"), UserNotificationState.Read);
        await SeedNotificationAsync(new MessageNotificationData("still unread"), UserNotificationState.Unread);

        using (ChangeCurrentUser(_userId))
        {
            await WithUnitOfWorkAsync(() =>
                GetRequiredService<IUserNotificationAppService>().DeleteAllReadAsync());

            await WithUnitOfWorkAsync(async () =>
            {
                var appService = GetRequiredService<IUserNotificationAppService>();
                (await appService.GetListAsync(new GetUserNotificationListInput())).TotalCount.ShouldBe(1);
                (await appService.GetUnreadCountAsync()).ShouldBe(1);
            });
        }
    }

    [Fact]
    public async Task Subscribing_reflects_in_available_subscriptions()
    {
        using (ChangeCurrentUser(_userId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await GetRequiredService<INotificationSubscriptionAppService>().SubscribeAsync(
                    new NotificationSubscriptionScopeDto { NotificationName = "order.shipped" });
            });

            await WithUnitOfWorkAsync(async () =>
            {
                var subscriptions = await GetRequiredService<INotificationSubscriptionAppService>().GetSubscriptionsAsync();
                var subscription = subscriptions.Items.Single(s => s.NotificationName == "order.shipped");
                subscription.IsSubscribed.ShouldBeTrue();
                subscription.DisplayName.ShouldBe("Order Shipped");
            });
        }
    }

    [Fact]
    public async Task Scoped_subscriptions_round_trip_and_unsubscribe_by_complete_identity()
    {
        using (ChangeCurrentUser(_userId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var appService = GetRequiredService<INotificationSubscriptionAppService>();
                await appService.SubscribeAsync(new NotificationSubscriptionScopeDto
                {
                    NotificationName = "order.shipped"
                });
                await appService.SubscribeAsync(new NotificationSubscriptionScopeDto
                {
                    NotificationName = "order.shipped",
                    EntityTypeName = "Demo.Order",
                    EntityId = "42"
                });
                await appService.SubscribeAsync(new NotificationSubscriptionScopeDto
                {
                    NotificationName = "order.shipped",
                    EntityTypeName = "Demo.Order",
                    EntityId = "99"
                });
            });

            await WithUnitOfWorkAsync(async () =>
            {
                var appService = GetRequiredService<INotificationSubscriptionAppService>();
                var rows = (await appService.GetSubscriptionsAsync()).Items
                    .Where(subscription => subscription.NotificationName == "order.shipped")
                    .ToList();

                rows.Count.ShouldBe(3);
                rows.ShouldContain(subscription =>
                    subscription.EntityTypeName == null && subscription.EntityId == null
                    && subscription.IsSubscribed);
                rows.ShouldContain(subscription =>
                    subscription.EntityTypeName == "Demo.Order" && subscription.EntityId == "42"
                    && subscription.IsSubscribed);
                rows.ShouldContain(subscription =>
                    subscription.EntityTypeName == "Demo.Order" && subscription.EntityId == "99"
                    && subscription.IsSubscribed);

                await appService.UnsubscribeAsync(new NotificationSubscriptionScopeDto
                {
                    NotificationName = "order.shipped",
                    EntityTypeName = "Demo.Order",
                    EntityId = "42"
                });
            });

            await WithUnitOfWorkAsync(async () =>
            {
                var rows = (await GetRequiredService<INotificationSubscriptionAppService>().GetSubscriptionsAsync()).Items
                    .Where(subscription => subscription.NotificationName == "order.shipped")
                    .ToList();

                rows.Count.ShouldBe(2);
                rows.ShouldContain(subscription =>
                    subscription.EntityTypeName == null && subscription.EntityId == null
                    && subscription.IsSubscribed);
                rows.ShouldContain(subscription =>
                    subscription.EntityTypeName == "Demo.Order" && subscription.EntityId == "99"
                    && subscription.IsSubscribed);
                rows.ShouldNotContain(subscription => subscription.EntityId == "42");
            });
        }
    }

    [Theory]
    [InlineData(null, "42")]
    [InlineData("Demo.Order", null)]
    [InlineData("", "")]
    [InlineData("   ", "42")]
    [InlineData("Demo.Order", "   ")]
    public void Scoped_subscription_contract_rejects_partial_or_blank_entity_identity(
        string? entityTypeName,
        string? entityId)
    {
        var input = new NotificationSubscriptionScopeDto
        {
            NotificationName = "order.shipped",
            EntityTypeName = entityTypeName,
            EntityId = entityId
        };
        var validationResults = new List<ValidationResult>();

        Validator.TryValidateObject(input, new ValidationContext(input), validationResults, true).ShouldBeFalse();
        validationResults.ShouldContain(result => result.MemberNames.Any(member =>
            member == nameof(NotificationSubscriptionScopeDto.EntityTypeName)
            || member == nameof(NotificationSubscriptionScopeDto.EntityId)));
    }
}
