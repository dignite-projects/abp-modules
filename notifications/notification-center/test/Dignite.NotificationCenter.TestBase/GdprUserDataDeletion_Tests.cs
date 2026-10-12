using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Abp.Notifications;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Gdpr;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>
/// Provider-agnostic scenarios for erasing a user's data on <see cref="GdprUserDataDeletionRequestedEto"/> — run
/// against both EF Core and MongoDB. The ABP version this module is built on gives the event a user id and nothing
/// else, so the user is erased wherever the data is.
/// </summary>
public abstract class GdprUserDataDeletion_Tests<TStartupModule> : NotificationCenterTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const int InboxRowsPerSeed = 2;
    private const int SubscriptionsPerSeed = 2;
    private const int DevicesPerSeed = 1;

    private readonly Guid _erased = Guid.NewGuid();
    private readonly Guid _bystander = Guid.NewGuid();
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _otherTenant = Guid.NewGuid();

    /// <summary>Everything the module keeps about one user in one tenant.</summary>
    private sealed record Footprint(long Inbox, int Subscriptions, int Devices);

    /// <summary>Gives the user an unread and a read inbox row, two subscriptions and one device in the tenant.</summary>
    private async Task SeedAsync(Guid userId, Guid? tenantId)
    {
        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var store = GetRequiredService<INotificationStore>();

                foreach (var state in new[] { UserNotificationState.Unread, UserNotificationState.Read })
                {
                    var notificationId = Guid.NewGuid();
                    var creationTime = DateTime.UtcNow;
                    await store.InsertNotificationAsync(new NotificationInfo
                    {
                        Id = notificationId,
                        NotificationName = TestNotificationDefinitionProvider.OrderShipped,
                        DataJson = SerializeData(new OrderShippedNotificationData { OrderNumber = "SO-1", ItemCount = 1 }),
                        Severity = NotificationSeverity.Info,
                        CreationTime = creationTime
                    });
                    await store.InsertUserNotificationAsync(new UserNotificationInfo
                    {
                        UserId = userId,
                        NotificationId = notificationId,
                        NotificationName = TestNotificationDefinitionProvider.OrderShipped,
                        State = state,
                        CreationTime = creationTime
                    });
                }

                await store.InsertSubscriptionAsync(new NotificationSubscriptionInfo
                {
                    UserId = userId,
                    NotificationName = TestNotificationDefinitionProvider.OrderShipped
                });
                await store.InsertSubscriptionAsync(new NotificationSubscriptionInfo
                {
                    UserId = userId,
                    NotificationName = TestNotificationDefinitionProvider.OrderShipped,
                    EntityTypeName = "Demo.Order",
                    EntityId = "42"
                });

                await GetRequiredService<PushDeviceManager>().RegisterAsync(
                    userId, "Expo", $"ExponentPushToken[{Guid.NewGuid():N}]", "en", null);
            });
        }
    }

    /// <summary>What the user has left in the tenant, read with the tenant filter on.</summary>
    private async Task<Footprint> GetFootprintAsync(Guid userId, Guid? tenantId)
    {
        Footprint footprint = null!;
        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                footprint = new Footprint(
                    await GetRequiredService<IRepository<UserNotification, Guid>>().CountAsync(x => x.UserId == userId),
                    (await GetRequiredService<INotificationStore>().GetSubscriptionsAsync(userId)).Count,
                    (await GetRequiredService<PushDeviceManager>().GetListAsync(userId)).Count);
            });
        }

        return footprint;
    }

    private async Task<long> CountPayloadsAsync(Guid? tenantId)
    {
        long count = 0;
        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            await WithUnitOfWorkAsync(async () =>
                count = await GetRequiredService<IRepository<Notification, Guid>>().GetCountAsync());
        }

        return count;
    }

    /// <summary>
    /// Hands the event to the registered handler, as the bus would. The bus itself is not used because a test host may
    /// route it through an outbox nothing drains (the EF Core one does); that the bus subscribes the handler is
    /// asserted separately.
    /// </summary>
    private Task RequestDeletionAsync(Guid userId)
    {
        return GetRequiredService<IDistributedEventHandler<GdprUserDataDeletionRequestedEto>>()
            .HandleEventAsync(new GdprUserDataDeletionRequestedEto { UserId = userId });
    }

    private static Footprint Seeded => new(InboxRowsPerSeed, SubscriptionsPerSeed, DevicesPerSeed);

    private static Footprint Erased => new(0, 0, 0);

    [Fact]
    public void The_handler_is_subscribed_to_the_distributed_event_bus()
    {
        // The handler is registered behind a unit-of-work proxy, so assert on what the bus subscribes, not on the
        // runtime type of the resolved instance.
        GetRequiredService<IOptions<AbpDistributedEventBusOptions>>().Value.Handlers
            .ShouldContain(typeof(GdprUserDataDeletionRequestedHandler));
        GetRequiredService<IEnumerable<IDistributedEventHandler<GdprUserDataDeletionRequestedEto>>>()
            .ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Erases_the_inbox_subscriptions_and_devices_of_the_user()
    {
        await SeedAsync(_erased, _tenant);

        // The unread and the read inbox row both exist before the request.
        (await GetFootprintAsync(_erased, _tenant)).ShouldBe(Seeded);

        await RequestDeletionAsync(_erased);

        (await GetFootprintAsync(_erased, _tenant)).ShouldBe(Erased);
    }

    [Fact]
    public async Task Erases_the_user_in_every_scope_and_leaves_other_users_untouched()
    {
        await SeedAsync(_erased, _tenant);
        await SeedAsync(_erased, _otherTenant);
        await SeedAsync(_erased, null);
        await SeedAsync(_bystander, _tenant);
        await SeedAsync(_bystander, null);

        await RequestDeletionAsync(_erased);

        (await GetFootprintAsync(_erased, _tenant)).ShouldBe(Erased);
        (await GetFootprintAsync(_erased, _otherTenant)).ShouldBe(Erased);
        (await GetFootprintAsync(_erased, null)).ShouldBe(Erased);
        (await GetFootprintAsync(_bystander, _tenant)).ShouldBe(Seeded);
        (await GetFootprintAsync(_bystander, null)).ShouldBe(Seeded);
    }

    [Fact]
    public async Task Erases_the_user_whichever_tenant_is_ambient_when_the_event_is_handled()
    {
        await SeedAsync(_erased, _tenant);

        // A distributed consumer handles the event in whatever tenant it happens to run in.
        using (GetRequiredService<ICurrentTenant>().Change(_otherTenant))
        {
            await RequestDeletionAsync(_erased);
        }

        (await GetFootprintAsync(_erased, _tenant)).ShouldBe(Erased);
    }

    [Fact]
    public async Task Keeps_the_shared_notification_payloads()
    {
        await SeedAsync(_erased, _tenant);
        await SeedAsync(_bystander, _tenant);
        var payloads = await CountPayloadsAsync(_tenant);
        payloads.ShouldBe(2 * InboxRowsPerSeed);

        await RequestDeletionAsync(_erased);

        // The payload has no owner and is shared by every recipient's inbox row; the host's retention job removes
        // orphans, so erasure never does.
        (await CountPayloadsAsync(_tenant)).ShouldBe(payloads);
    }

    [Fact]
    public async Task A_repeated_request_and_a_user_without_data_are_not_errors()
    {
        await SeedAsync(_erased, _tenant);

        await RequestDeletionAsync(_erased);
        await RequestDeletionAsync(_erased);
        await RequestDeletionAsync(Guid.NewGuid());

        (await GetFootprintAsync(_erased, _tenant)).ShouldBe(Erased);
    }
}
