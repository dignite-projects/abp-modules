using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>
/// Provider-agnostic <see cref="INotificationStore"/> scenarios. The concrete EF Core and MongoDB
/// test projects each derive a thin subclass bound to their own startup module, so these exact
/// assertions run against both persistence providers.
/// </summary>
public abstract class NotificationStore_Tests<TStartupModule> : NotificationCenterTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private async Task InsertAsync(Guid notificationId, Guid userId, NotificationData data,
        UserNotificationState state = UserNotificationState.Unread)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            var info = new NotificationInfo
            {
                Id = notificationId,
                NotificationName = "order.shipped",
                Data = data,
                Severity = NotificationSeverity.Success,
                CreationTime = DateTime.UtcNow
            };
            await store.InsertNotificationAsync(info);
            await store.InsertUserNotificationAsync(new UserNotificationInfo
            {
                UserId = userId,
                NotificationId = notificationId,
                State = state,
                CreationTime = info.CreationTime
            });
        });
    }

    [Fact]
    public async Task Round_trips_custom_notification_data_through_the_store()
    {
        var notificationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await InsertAsync(notificationId, userId,
            new OrderShippedNotificationData { OrderNumber = "SO-1001", ItemCount = 3 });

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            var list = await store.GetUserNotificationsAsync(userId);

            list.Count.ShouldBe(1);
            var row = list.Single();
            row.Notification.NotificationName.ShouldBe("order.shipped");
            var data = row.Notification.Data.ShouldBeOfType<OrderShippedNotificationData>();
            data.OrderNumber.ShouldBe("SO-1001");
            data.ItemCount.ShouldBe(3);
            row.UserNotification.State.ShouldBe(UserNotificationState.Unread);
        });
    }

    [Fact]
    public async Task Persisted_data_uses_the_discriminator_and_no_assembly_qualified_name()
    {
        var notificationId = Guid.NewGuid();
        await InsertAsync(notificationId, Guid.NewGuid(),
            new OrderShippedNotificationData { OrderNumber = "SO-9", ItemCount = 1 });

        await WithUnitOfWorkAsync(async () =>
        {
            var repository = GetRequiredService<IRepository<Notification, Guid>>();
            var entity = await repository.GetAsync(notificationId);

            entity.Data.ShouldNotBeNull();
            entity.Data!.ShouldContain("\"type\":\"Test.OrderShipped\"");
            entity.Data.ShouldNotContain("Version=");
            entity.Data.ShouldNotContain("OrderShippedNotificationData");
        });
    }

    [Fact]
    public async Task User_notification_inserts_are_idempotent_by_user_and_notification()
    {
        var notificationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var creationTime = DateTime.UtcNow;

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            await store.InsertNotificationAsync(new NotificationInfo
            {
                Id = notificationId,
                NotificationName = "order.shipped",
                Data = new OrderShippedNotificationData { OrderNumber = "SO-10", ItemCount = 1 },
                Severity = NotificationSeverity.Info,
                CreationTime = creationTime
            });

            await store.InsertUserNotificationAsync(new UserNotificationInfo
            {
                UserId = userId,
                NotificationId = notificationId,
                State = UserNotificationState.Unread,
                CreationTime = creationTime
            });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            await store.InsertUserNotificationAsync(new UserNotificationInfo
            {
                UserId = userId,
                NotificationId = notificationId,
                State = UserNotificationState.Read,
                CreationTime = creationTime
            });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            await store.InsertUserNotificationsAsync(new[]
            {
                new UserNotificationInfo
                {
                    UserId = userId,
                    NotificationId = notificationId,
                    State = UserNotificationState.Unread,
                    CreationTime = creationTime
                },
                new UserNotificationInfo
                {
                    UserId = otherUserId,
                    NotificationId = notificationId,
                    State = UserNotificationState.Unread,
                    CreationTime = creationTime
                },
                new UserNotificationInfo
                {
                    UserId = otherUserId,
                    NotificationId = notificationId,
                    State = UserNotificationState.Read,
                    CreationTime = creationTime
                }
            });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var rows = await GetRequiredService<IRepository<UserNotification, Guid>>()
                .GetListAsync(row => row.NotificationId == notificationId);

            rows.Count.ShouldBe(2);
            rows.Select(row => row.UserId).ShouldBe(new[] { userId, otherUserId }, ignoreOrder: true);
            rows.Single(row => row.UserId == userId).State.ShouldBe(UserNotificationState.Unread);
            rows.Single(row => row.UserId == otherUserId).State.ShouldBe(UserNotificationState.Unread);
        });
    }

    [Fact]
    public async Task Historical_payload_fixtures_degrade_individually_without_failing_the_inbox_page()
    {
        var userId = Guid.NewGuid();
        var legacyId = Guid.NewGuid();
        var unknownId = Guid.NewGuid();
        var malformedId = Guid.NewGuid();
        var throwingSetterId = Guid.NewGuid();
        await InsertRawAsync(
            legacyId,
            userId,
            HistoricalPayloadFixtures.Read("legacy-order-shipped-v1.json"),
            DateTime.UtcNow.AddMinutes(-4));
        await InsertRawAsync(
            unknownId,
            userId,
            HistoricalPayloadFixtures.Read("unknown-payload-v1.json"),
            DateTime.UtcNow.AddMinutes(-3));
        await InsertRawAsync(
            malformedId,
            userId,
            HistoricalPayloadFixtures.Read("malformed-order-shipped-v1.json"),
            DateTime.UtcNow.AddMinutes(-1));
        await InsertRawAsync(
            throwingSetterId,
            userId,
            HistoricalPayloadFixtures.Read("malformed-throwing-order-v1.json"),
            DateTime.UtcNow);

        await WithUnitOfWorkAsync(async () =>
        {
            var rows = await GetRequiredService<INotificationStore>().GetUserNotificationsAsync(userId);

            rows.Count.ShouldBe(4);
            var legacy = rows.Single(row => row.Notification.Id == legacyId)
                .Notification.Data.ShouldBeOfType<OrderShippedNotificationData>();
            legacy.OrderNumber.ShouldBe("SO-LEGACY");

            var unknown = rows.Single(row => row.Notification.Id == unknownId)
                .Notification.Data.ShouldBeOfType<UnsupportedNotificationData>();
            unknown.Reason.ShouldBe(UnsupportedNotificationDataReason.UnknownDiscriminator);
            unknown.OriginalDiscriminator.ShouldBe("Removed.Module.Payload");

            var malformed = rows.Single(row => row.Notification.Id == malformedId)
                .Notification.Data.ShouldBeOfType<UnsupportedNotificationData>();
            malformed.Reason.ShouldBe(UnsupportedNotificationDataReason.MalformedPayload);
            malformed.RawJson.ShouldContain("not-an-integer");
            var throwingSetter = rows.Single(row => row.Notification.Id == throwingSetterId)
                .Notification.Data.ShouldBeOfType<UnsupportedNotificationData>();
            throwingSetter.Reason.ShouldBe(UnsupportedNotificationDataReason.MalformedPayload);
            throwingSetter.RawJson.ShouldContain("THROW-FORMAT");
        });
    }

    [Fact]
    public async Task Filters_inbox_by_state_and_counts()
    {
        var userId = Guid.NewGuid();
        await InsertAsync(Guid.NewGuid(), userId, new MessageNotificationData("a"), UserNotificationState.Unread);
        await InsertAsync(Guid.NewGuid(), userId, new MessageNotificationData("b"), UserNotificationState.Read);

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();

            (await store.GetUserNotificationCountAsync(userId)).ShouldBe(2);
            (await store.GetUserNotificationCountAsync(userId, UserNotificationState.Unread)).ShouldBe(1);

            var unread = await store.GetUserNotificationsAsync(userId, UserNotificationState.Unread);
            unread.Count.ShouldBe(1);
        });
    }

    [Fact]
    public async Task Subscribes_and_queries_subscriptions()
    {
        var userId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            await store.InsertSubscriptionAsync(new NotificationSubscriptionInfo
            {
                UserId = userId,
                NotificationName = "order.shipped"
            });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();

            (await store.IsSubscribedAsync(userId, "order.shipped", null, null)).ShouldBeTrue();
            (await store.GetSubscriptionsAsync("order.shipped", null, null))
                .ShouldContain(s => s.UserId == userId);
            (await store.GetSubscriptionsAsync(userId)).Count.ShouldBe(1);
        });
    }

    [Fact]
    public async Task Definition_wide_and_entity_scopes_coexist_match_and_delete_independently()
    {
        var bothScopes = Guid.NewGuid();
        var exactOrder42 = Guid.NewGuid();
        var exactOrder99 = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            await store.InsertSubscriptionAsync(NewSubscription(bothScopes));
            await store.InsertSubscriptionAsync(NewSubscription(bothScopes, "Demo.Order", "42"));
            await store.InsertSubscriptionAsync(NewSubscription(exactOrder42, "Demo.Order", "42"));
            await store.InsertSubscriptionAsync(NewSubscription(exactOrder99, "Demo.Order", "99"));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();

            (await store.GetSubscriptionsAsync("order.shipped", null, null))
                .Select(subscription => subscription.UserId)
                .ShouldBe(new[] { bothScopes }, ignoreOrder: true);
            (await store.GetSubscriptionsAsync("order.shipped", "Demo.Order", "42"))
                .Select(subscription => subscription.UserId)
                .ShouldBe(new[] { bothScopes, bothScopes, exactOrder42 }, ignoreOrder: true);
            (await store.GetSubscriptionsAsync("order.shipped", "Demo.Order", "99"))
                .Select(subscription => subscription.UserId)
                .ShouldBe(new[] { bothScopes, exactOrder99 }, ignoreOrder: true);

            await store.DeleteSubscriptionAsync(bothScopes, "order.shipped", "Demo.Order", "42");
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            (await store.IsSubscribedAsync(bothScopes, "order.shipped", null, null)).ShouldBeTrue();
            (await store.IsSubscribedAsync(bothScopes, "order.shipped", "Demo.Order", "42")).ShouldBeFalse();
            (await store.GetSubscriptionsAsync(bothScopes)).ShouldHaveSingleItem()
                .EntityTypeName.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Batched_subscription_pages_are_globally_distinct_stable_and_exact()
    {
        var users = Enumerable.Range(0, 5)
            .Select(_ => Guid.NewGuid())
            .OrderBy(userId => userId)
            .ToArray();

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            await store.InsertSubscriptionAsync(NewSubscription(users[0]));
            await store.InsertSubscriptionAsync(NewSubscription(users[1]));
            await store.InsertSubscriptionAsync(NewSubscription(users[1], "Demo.Order", "42"));
            await store.InsertSubscriptionAsync(NewSubscription(users[2], "Demo.Order", "42"));
            await store.InsertSubscriptionAsync(NewSubscription(users[3], "Demo.Order", "42"));
            await store.InsertSubscriptionAsync(NewSubscription(users[4], "Demo.Order", "99"));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            var firstPage = await store.GetSubscriptionUserIdsAsync(
                "order.shipped", "Demo.Order", "42", null, 2);
            var secondPage = await store.GetSubscriptionUserIdsAsync(
                "order.shipped", "Demo.Order", "42", firstPage[^1], 2);
            var exactBoundaryPage = await store.GetSubscriptionUserIdsAsync(
                "order.shipped", "Demo.Order", "42", secondPage[^1], 2);

            firstPage.ShouldBe(users.Take(2));
            secondPage.ShouldBe(users.Skip(2).Take(2));
            exactBoundaryPage.ShouldBeEmpty();
            firstPage.Concat(secondPage).Distinct().Count().ShouldBe(4);
        });
    }

    [Fact]
    public async Task Subscription_keyset_does_not_repeat_or_skip_when_rows_before_the_cursor_change()
    {
        var a = Guid.Parse("10000000-0000-0000-0000-000000000000");
        var insertedBeforeCursor = Guid.Parse("20000000-0000-0000-0000-000000000000");
        var b = Guid.Parse("30000000-0000-0000-0000-000000000000");
        var c = Guid.Parse("50000000-0000-0000-0000-000000000000");
        var insertedAfterCursor = Guid.Parse("60000000-0000-0000-0000-000000000000");
        var d = Guid.Parse("70000000-0000-0000-0000-000000000000");

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            foreach (var userId in new[] { a, b, c, d })
            {
                await store.InsertSubscriptionAsync(NewSubscription(userId));
            }
        });

        List<Guid>? firstPage = null;
        await WithUnitOfWorkAsync(async () =>
        {
            firstPage = await GetRequiredService<INotificationStore>()
                .GetSubscriptionUserIdsAsync("order.shipped", null, null, null, 2);
        });
        firstPage.ShouldBe(new[] { a, b });

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            await store.DeleteSubscriptionAsync(a, "order.shipped", null, null);
            await store.InsertSubscriptionAsync(NewSubscription(insertedBeforeCursor));
            await store.InsertSubscriptionAsync(NewSubscription(insertedAfterCursor));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var secondPage = await GetRequiredService<INotificationStore>()
                .GetSubscriptionUserIdsAsync("order.shipped", null, null, firstPage![^1], 10);

            secondPage.ShouldBe(new[] { c, insertedAfterCursor, d });
            secondPage.ShouldNotContain(b);
            secondPage.ShouldNotContain(insertedBeforeCursor);
        });
    }

    [Fact]
    public async Task Subscription_identity_is_unique_for_host_and_tenant_scopes_independently()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<INotificationStore>().InsertSubscriptionAsync(NewSubscription(userId));
        });

        using (GetRequiredService<ICurrentTenant>().Change(tenantId, "tenant"))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await GetRequiredService<INotificationStore>().InsertSubscriptionAsync(NewSubscription(userId));
            });
            await WithUnitOfWorkAsync(async () =>
            {
                (await GetRequiredService<INotificationStore>().GetSubscriptionsAsync(userId))
                    .ShouldHaveSingleItem().TenantId.ShouldBe(tenantId);
            });
        }

        await WithUnitOfWorkAsync(async () =>
        {
            (await GetRequiredService<INotificationStore>().GetSubscriptionsAsync(userId))
                .ShouldHaveSingleItem().TenantId.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Duplicate_subscription_identity_is_rejected()
    {
        var userId = Guid.NewGuid();

        await Should.ThrowAsync<Exception>(() => WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            await store.InsertSubscriptionAsync(NewSubscription(userId, "Demo.Order", "42"));
            await store.InsertSubscriptionAsync(NewSubscription(userId, "Demo.Order", "42"));
        }));
    }

    [Fact]
    public async Task Subscription_name_identity_is_ordinal_and_case_sensitive()
    {
        var userId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            await store.InsertSubscriptionAsync(NewSubscription(userId));
            var caseVariant = NewSubscription(userId);
            caseVariant.NotificationName = "Order.Shipped";
            await store.InsertSubscriptionAsync(caseVariant);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var store = GetRequiredService<INotificationStore>();
            (await store.GetSubscriptionsAsync("order.shipped", null, null)).ShouldHaveSingleItem()
                .NotificationName.ShouldBe("order.shipped");
            (await store.GetSubscriptionsAsync("Order.Shipped", null, null)).ShouldHaveSingleItem()
                .NotificationName.ShouldBe("Order.Shipped");
        });
    }

    [Fact]
    public async Task Partial_entity_scope_is_rejected()
    {
        await Should.ThrowAsync<ArgumentException>(() => WithUnitOfWorkAsync(() =>
            GetRequiredService<INotificationStore>().InsertSubscriptionAsync(
                NewSubscription(Guid.NewGuid(), "Demo.Order", null))));
    }

    private static NotificationSubscriptionInfo NewSubscription(
        Guid userId,
        string? entityTypeName = null,
        string? entityId = null)
    {
        return new NotificationSubscriptionInfo
        {
            UserId = userId,
            NotificationName = "order.shipped",
            EntityTypeName = entityTypeName,
            EntityId = entityId,
            CreationTime = DateTime.UtcNow
        };
    }

    private async Task InsertRawAsync(
        Guid notificationId,
        Guid userId,
        string data,
        DateTime creationTime)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<IRepository<Notification, Guid>>().InsertAsync(
                new Notification(
                    notificationId,
                    "order.shipped",
                    data,
                    null,
                    null,
                    NotificationSeverity.Info,
                    creationTime,
                    null));
            await GetRequiredService<IRepository<UserNotification, Guid>>().InsertAsync(
                new UserNotification(
                    Guid.NewGuid(),
                    userId,
                    notificationId,
                    UserNotificationState.Unread,
                    creationTime,
                    null));
        });
    }
}
