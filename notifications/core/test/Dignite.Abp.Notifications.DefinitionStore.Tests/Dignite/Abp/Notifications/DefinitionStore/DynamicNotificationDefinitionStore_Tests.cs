using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Localization;
using Xunit;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>
/// A notification service that defines nothing reads what the publishers saved: the requirements as they were saved,
/// a reload when the common stamp changes (and not before), and its own definitions first.
/// </summary>
public class DynamicNotificationDefinitionStore_Tests
{
    [Fact]
    public async Task The_notification_service_reads_what_a_publisher_saved()
    {
        using var shared = new SharedDefinitionStoreInfrastructure();
        await SavePublisherAsync<PublisherATestModule>("PublisherA", shared);

        await using var notificationService =
            await DefinitionStoreTestApplication.StartAsync<NotificationServiceTestModule>("NotificationService", shared);
        var manager = notificationService.Get<INotificationDefinitionManager>();

        var shipped = await manager.GetAsync(PublisherADefinitionProvider.OrderShipped);
        shipped.GroupName.ShouldBe(PublisherADefinitionProvider.GroupName);
        shipped.PermissionName.ShouldBe(PublisherADefinitionProvider.OrdersPermission);
        shipped.FeatureName.ShouldBeNull();
        var displayName = shipped.DisplayName.ShouldBeOfType<LocalizableString>();
        displayName.ResourceName.ShouldBe(PublisherADefinitionProvider.ResourceName);
        displayName.Name.ShouldBe("Notification:OrderShipped");
        shipped.Description.ShouldBeOfType<LocalizableString>().Name.ShouldBe("Notification:OrderShipped:Description");
        shipped.Attributes["Category"].ShouldBe("orders");
        Convert.ToInt32(shipped.Attributes["Priority"]).ShouldBe(2);
        shipped.Attributes.ShouldNotContainKey("Lines");

        (await manager.GetAsync(PublisherADefinitionProvider.OrderCancelled)).FeatureName
            .ShouldBe(PublisherADefinitionProvider.OrdersFeature);

        var group = (await manager.GetGroupsAsync()).ShouldHaveSingleItem();
        group.Name.ShouldBe(PublisherADefinitionProvider.GroupName);
        group.Notifications.Select(notification => notification.Name).OrderBy(name => name, StringComparer.Ordinal)
            .ShouldBe(new[] { PublisherADefinitionProvider.OrderCancelled, PublisherADefinitionProvider.OrderShipped });
    }

    [Fact]
    public async Task A_changed_stamp_makes_the_store_reload_after_its_check_window()
    {
        using var shared = new SharedDefinitionStoreInfrastructure();
        await SavePublisherAsync<PublisherATestModule>("PublisherA", shared);

        await using var notificationService =
            await DefinitionStoreTestApplication.StartAsync<NotificationServiceTestModule>("NotificationService", shared);
        var manager = notificationService.Get<INotificationDefinitionManager>();
        (await manager.GetOrNullAsync(PublisherADefinitionProvider.OrderShipped)).ShouldNotBeNull();

        // Publisher B saves: new records, and a new stamp.
        await SavePublisherAsync<PublisherBTestModule>("PublisherB", shared);

        // Within the 30-second window the store does not even look at the stamp.
        (await manager.GetOrNullAsync(PublisherBDefinitionProvider.DocumentReady)).ShouldBeNull();

        notificationService.ExpireDynamicStoreCheckWindow();
        (await manager.GetOrNullAsync(PublisherBDefinitionProvider.DocumentReady)).ShouldNotBeNull();
        (await manager.GetAllAsync()).Select(definition => definition.Name).ShouldContain(PublisherADefinitionProvider.OrderShipped);
    }

    [Fact]
    public async Task An_unchanged_stamp_keeps_the_loaded_definitions()
    {
        using var shared = new SharedDefinitionStoreInfrastructure();
        await SavePublisherAsync<PublisherATestModule>("PublisherA", shared);

        await using var notificationService =
            await DefinitionStoreTestApplication.StartAsync<NotificationServiceTestModule>("NotificationService", shared);
        var manager = notificationService.Get<INotificationDefinitionManager>();
        (await manager.GetAllAsync()).Count.ShouldBe(2);

        // A row written without renewing the stamp (no saver does that) is not picked up: the stamp, not the table,
        // says when to reload.
        await notificationService.WithUnitOfWorkAsync(async services =>
        {
            await services.GetRequiredService<INotificationDefinitionRecordRepository>().InsertAsync(
                new NotificationDefinitionRecord(
                    Guid.NewGuid(),
                    PublisherADefinitionProvider.GroupName,
                    "PublisherA.Unannounced",
                    "F:Unannounced"));
        });

        notificationService.ExpireDynamicStoreCheckWindow();
        (await manager.GetOrNullAsync("PublisherA.Unannounced")).ShouldBeNull();
    }

    [Fact]
    public async Task A_missing_stamp_is_created_on_the_first_read()
    {
        using var shared = new SharedDefinitionStoreInfrastructure();

        await using var notificationService =
            await DefinitionStoreTestApplication.StartAsync<NotificationServiceTestModule>("NotificationService", shared);

        (await notificationService.Get<INotificationDefinitionManager>().GetAllAsync()).ShouldBeEmpty();

        var stamp = await shared.Cache.GetStringAsync(shared.StampKey);
        stamp.ShouldNotBeNullOrEmpty();
        notificationService.Get<IDynamicNotificationDefinitionStoreInMemoryCache>().CacheStamp.ShouldBe(stamp);
    }

    [Fact]
    public async Task With_the_dynamic_store_off_nothing_is_read()
    {
        using var shared = new SharedDefinitionStoreInfrastructure();
        await SavePublisherAsync<PublisherATestModule>("PublisherA", shared);

        await using var notificationService = await DefinitionStoreTestApplication.StartAsync<NotificationServiceTestModule>(
            "NotificationService",
            shared,
            services => services.PostConfigure<NotificationDefinitionStoreOptions>(options =>
                options.IsDynamicNotificationStoreEnabled = false));

        (await notificationService.Get<INotificationDefinitionManager>().GetOrNullAsync(PublisherADefinitionProvider.OrderShipped))
            .ShouldBeNull();
        (await shared.Cache.GetStringAsync(shared.StampKey)).ShouldNotBeNull();
        notificationService.Get<IDynamicNotificationDefinitionStoreInMemoryCache>().CacheStamp.ShouldBeNull();
    }

    [Fact]
    public async Task A_static_definition_wins_over_the_saved_one_of_the_same_name()
    {
        using var shared = new SharedDefinitionStoreInfrastructure();
        await SavePublisherAsync<PublisherATestModule>("PublisherA", shared);

        // This service does not save, so the publisher's records stay as they are.
        await using var notificationService =
            await DefinitionStoreTestApplication.StartAsync<NotificationServiceWithLocalCopyTestModule>(
                "NotificationService",
                shared);
        var manager = notificationService.Get<INotificationDefinitionManager>();

        var shipped = await manager.GetAsync(PublisherADefinitionProvider.OrderShipped);
        shipped.DisplayName.ShouldBeOfType<FixedLocalizableString>().Value.ShouldBe("Shipped (local)");
        shipped.PermissionName.ShouldBeNull();

        // The saved definition the service does not define itself is still there, and each name appears once.
        (await manager.GetAllAsync()).Select(definition => definition.Name)
            .ShouldBe(new[] { PublisherADefinitionProvider.OrderShipped, PublisherADefinitionProvider.OrderCancelled });
        var group = (await manager.GetGroupsAsync()).ShouldHaveSingleItem();
        group.DisplayName.ShouldBeOfType<FixedLocalizableString>().Value.ShouldBe("Orders (local)");
        (await manager.GetAsync(PublisherADefinitionProvider.OrderCancelled)).GroupName.ShouldBe(group.Name);
    }

    private static async Task SavePublisherAsync<TModule>(string applicationName, SharedDefinitionStoreInfrastructure shared)
        where TModule : Volo.Abp.Modularity.IAbpModule
    {
        await using var publisher = await DefinitionStoreTestApplication.StartAsync<TModule>(applicationName, shared);
        await publisher.SaveStaticDefinitionsAsync();
    }
}
