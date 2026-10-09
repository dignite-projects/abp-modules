using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>
/// Several services save their definitions to the same tables: each one inserts and patches only what it defines,
/// deletes only what it lists, and skips the save when nothing changed since its last one.
/// </summary>
public class StaticNotificationDefinitionSaver_Tests
{
    [Fact]
    public async Task Two_applications_save_side_by_side_and_neither_deletes_the_others_records()
    {
        using var shared = new SharedDefinitionStoreInfrastructure();

        await using (var publisherA = await DefinitionStoreTestApplication.StartAsync<PublisherATestModule>("PublisherA", shared))
        {
            await publisherA.SaveStaticDefinitionsAsync();
        }

        await using (var publisherB = await DefinitionStoreTestApplication.StartAsync<PublisherBTestModule>("PublisherB", shared))
        {
            await publisherB.SaveStaticDefinitionsAsync();

            (await publisherB.GetStoredNotificationNamesAsync()).ShouldBe(new[]
            {
                PublisherADefinitionProvider.OrderCancelled,
                PublisherADefinitionProvider.OrderShipped,
                PublisherBDefinitionProvider.DocumentReady
            });
            (await publisherB.GetStoredGroupNamesAsync()).ShouldBe(new[]
            {
                PublisherADefinitionProvider.GroupName,
                PublisherBDefinitionProvider.GroupName
            });
        }

        // Publisher B starts again without its definition (removed from code, not listed as deleted): its record
        // stays, exactly as ABP keeps a permission nobody lists in DeletedPermissions.
        await using var publisherBWithoutDefinitions =
            await DefinitionStoreTestApplication.StartAsync<NotificationServiceTestModule>("PublisherB", shared);
        await publisherBWithoutDefinitions.SaveStaticDefinitionsAsync();

        (await publisherBWithoutDefinitions.GetStoredNotificationNamesAsync())
            .ShouldContain(PublisherBDefinitionProvider.DocumentReady);
    }

    [Fact]
    public async Task DeletedNotifications_removes_only_the_listed_definitions()
    {
        using var shared = new SharedDefinitionStoreInfrastructure();
        await SaveBothPublishersAsync(shared);

        // Publisher A after OrderCancelled was removed from its code and listed for deletion.
        await using var publisherA = await DefinitionStoreTestApplication.StartAsync<NotificationServiceTestModule>(
            "PublisherA",
            shared,
            services => services.Configure<NotificationDefinitionStoreOptions>(options =>
                options.DeletedNotifications.Add(PublisherADefinitionProvider.OrderCancelled)));
        await publisherA.SaveStaticDefinitionsAsync();

        (await publisherA.GetStoredNotificationNamesAsync()).ShouldBe(new[]
        {
            PublisherADefinitionProvider.OrderShipped,
            PublisherBDefinitionProvider.DocumentReady
        });
        (await publisherA.GetStoredGroupNamesAsync()).ShouldContain(PublisherADefinitionProvider.GroupName);
    }

    [Fact]
    public async Task DeletedNotificationGroups_removes_the_group_and_its_definitions()
    {
        using var shared = new SharedDefinitionStoreInfrastructure();
        await SaveBothPublishersAsync(shared);

        await using var publisherA = await DefinitionStoreTestApplication.StartAsync<NotificationServiceTestModule>(
            "PublisherA",
            shared,
            services => services.Configure<NotificationDefinitionStoreOptions>(options =>
                options.DeletedNotificationGroups.Add(PublisherADefinitionProvider.GroupName)));
        await publisherA.SaveStaticDefinitionsAsync();

        (await publisherA.GetStoredNotificationNamesAsync()).ShouldBe(new[] { PublisherBDefinitionProvider.DocumentReady });
        (await publisherA.GetStoredGroupNamesAsync()).ShouldBe(new[] { PublisherBDefinitionProvider.GroupName });
    }

    [Fact]
    public async Task An_unchanged_hash_skips_the_save()
    {
        using var shared = new SharedDefinitionStoreInfrastructure();

        await using var publisherA =
            await DefinitionStoreTestApplication.StartAsync<PublisherATestModule>("PublisherA", shared);
        await publisherA.SaveStaticDefinitionsAsync();
        var stamp = await shared.Cache.GetStringAsync(shared.StampKey);
        stamp.ShouldNotBeNull();

        // Drift the table behind the saver's back: if the save ran, it would insert the record again.
        await DeleteNotificationRecordAsync(publisherA, PublisherADefinitionProvider.OrderShipped);

        await publisherA.SaveStaticDefinitionsAsync();
        await using (var restartedPublisherA =
                     await DefinitionStoreTestApplication.StartAsync<PublisherATestModule>("PublisherA", shared))
        {
            await restartedPublisherA.SaveStaticDefinitionsAsync();
        }

        (await publisherA.GetStoredNotificationNamesAsync())
            .ShouldNotContain(PublisherADefinitionProvider.OrderShipped);
        (await shared.Cache.GetStringAsync(shared.StampKey)).ShouldBe(stamp);

        // The hash is the only guard: once it is gone (a flushed cache), the next save writes again.
        await shared.Cache.RemoveAsync(shared.GetHashKey("PublisherA"));
        await publisherA.SaveStaticDefinitionsAsync();

        (await publisherA.GetStoredNotificationNamesAsync()).ShouldContain(PublisherADefinitionProvider.OrderShipped);
        (await shared.Cache.GetStringAsync(shared.StampKey)).ShouldNotBe(stamp);
    }

    [Fact]
    public async Task A_changed_definition_is_patched_and_renews_the_stamp()
    {
        using var shared = new SharedDefinitionStoreInfrastructure();

        await using (var publisherA = await DefinitionStoreTestApplication.StartAsync<PublisherATestModule>("PublisherA", shared))
        {
            await publisherA.SaveStaticDefinitionsAsync();
        }

        var stamp = await shared.Cache.GetStringAsync(shared.StampKey);

        // Another version of the same definition: new display text, no permission, no attributes.
        await using var changed =
            await DefinitionStoreTestApplication.StartAsync<NotificationServiceWithLocalCopyTestModule>("PublisherA", shared);
        await changed.SaveStaticDefinitionsAsync();

        var record = await FindNotificationRecordAsync(changed, PublisherADefinitionProvider.OrderShipped);
        record.DisplayName.ShouldBe("F:Shipped (local)");
        record.Description.ShouldBeNull();
        record.PermissionName.ShouldBeNull();
        record.ExtraProperties.ShouldBeEmpty();
        (await shared.Cache.GetStringAsync(shared.StampKey)).ShouldNotBe(stamp);
    }

    [Fact]
    public async Task Display_texts_are_saved_by_resource_name_and_only_json_scalar_attributes_are_kept()
    {
        using var shared = new SharedDefinitionStoreInfrastructure();
        await using var publisherA =
            await DefinitionStoreTestApplication.StartAsync<PublisherATestModule>("PublisherA", shared);
        await publisherA.SaveStaticDefinitionsAsync();

        var shipped = await FindNotificationRecordAsync(publisherA, PublisherADefinitionProvider.OrderShipped);
        shipped.GroupName.ShouldBe(PublisherADefinitionProvider.GroupName);
        shipped.DisplayName.ShouldBe("L:PublisherA,Notification:OrderShipped");
        shipped.Description.ShouldBe("L:PublisherA,Notification:OrderShipped:Description");
        shipped.PermissionName.ShouldBe(PublisherADefinitionProvider.OrdersPermission);
        shipped.FeatureName.ShouldBeNull();
        shipped.ExtraProperties.Keys.OrderBy(key => key, StringComparer.Ordinal)
            .ShouldBe(new[] { "Category", "Priority" });
        shipped.GetProperty<string>("Category").ShouldBe("orders");
        shipped.GetProperty<int>("Priority").ShouldBe(2);

        var cancelled = await FindNotificationRecordAsync(publisherA, PublisherADefinitionProvider.OrderCancelled);
        cancelled.DisplayName.ShouldBe("F:Order cancelled");
        cancelled.FeatureName.ShouldBe(PublisherADefinitionProvider.OrdersFeature);

        var group = await publisherA.WithUnitOfWorkAsync(async services =>
            (await services.GetRequiredService<INotificationGroupDefinitionRecordRepository>().GetListAsync()).Single());
        group.DisplayName.ShouldBe("L:PublisherA,Notification:Group:Orders");
    }

    private static async Task SaveBothPublishersAsync(SharedDefinitionStoreInfrastructure shared)
    {
        await using (var publisherA = await DefinitionStoreTestApplication.StartAsync<PublisherATestModule>("PublisherA", shared))
        {
            await publisherA.SaveStaticDefinitionsAsync();
        }

        await using (var publisherB = await DefinitionStoreTestApplication.StartAsync<PublisherBTestModule>("PublisherB", shared))
        {
            await publisherB.SaveStaticDefinitionsAsync();
        }
    }

    private static Task<NotificationDefinitionRecord> FindNotificationRecordAsync(
        DefinitionStoreTestApplication application,
        string name)
    {
        return application.WithUnitOfWorkAsync(async services =>
            (await services.GetRequiredService<INotificationDefinitionRecordRepository>().FindByNameAsync(name))!);
    }

    private static Task DeleteNotificationRecordAsync(DefinitionStoreTestApplication application, string name)
    {
        return application.WithUnitOfWorkAsync(async services =>
        {
            var repository = services.GetRequiredService<INotificationDefinitionRecordRepository>();
            await repository.DeleteAsync((await repository.FindByNameAsync(name))!);
        });
    }
}
