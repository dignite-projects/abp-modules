using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The module's wiring: the dynamic store replaces Abstractions' empty one, the startup initializer, migrations.
/// Provider-agnostic: each persistence provider's test project runs it on its own database.
/// </summary>
public abstract class AbpNotificationsDomainModule_Tests<TInfrastructure>
    where TInfrastructure : SharedDefinitionStoreInfrastructure, new()
{
    [Fact]
    public async Task The_definition_store_replaces_the_empty_dynamic_source()
    {
        using var shared = new TInfrastructure();
        await using var application =
            await DefinitionStoreTestApplication.StartAsync<NotificationServiceTestModule>("NotificationService", shared);

        application.Get<IDynamicNotificationDefinitionStore>().ShouldBeOfType<DynamicNotificationDefinitionStore>();
    }

    [Fact]
    public async Task The_initializer_saves_the_static_definitions_and_warms_the_dynamic_store()
    {
        using var shared = new TInfrastructure();
        await using var publisherA =
            await DefinitionStoreTestApplication.StartAsync<PublisherATestModule>("PublisherA", shared);

        await publisherA.Get<NotificationDynamicInitializer>().InitializeAsync(runInBackground: false);

        (await publisherA.GetStoredNotificationNamesAsync()).ShouldBe(new[]
        {
            PublisherADefinitionProvider.OrderCancelled,
            PublisherADefinitionProvider.OrderShipped
        });
        publisherA.Get<IDynamicNotificationDefinitionStoreInMemoryCache>().CacheStamp.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_data_migration_environment_neither_saves_nor_reads()
    {
        using var shared = new TInfrastructure();
        await using var migrator = await DefinitionStoreTestApplication.StartAsync<PublisherATestModule>(
            "Migrator",
            shared,
            services => services.AddDataMigrationEnvironment());

        var options = migrator.Get<IOptions<NotificationDefinitionStoreOptions>>().Value;
        options.SaveStaticNotificationsToDatabase.ShouldBeFalse();
        options.IsDynamicNotificationStoreEnabled.ShouldBeFalse();

        await migrator.Get<NotificationDynamicInitializer>().InitializeAsync(runInBackground: false);
        (await migrator.GetStoredNotificationNamesAsync()).ShouldBeEmpty();
    }
}
