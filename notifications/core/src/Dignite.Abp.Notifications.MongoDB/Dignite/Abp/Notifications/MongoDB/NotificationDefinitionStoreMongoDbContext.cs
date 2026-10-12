using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.Notifications.MongoDB;

/// <summary>
/// The definition store's own MongoDB context. Its collections and indexes come from
/// <see cref="NotificationDefinitionStoreMongoDbContextExtensions.ConfigureNotificationDefinitionStore"/>, which a host's
/// own context can call as well; there is nothing to migrate.
/// </summary>
[IgnoreMultiTenancy]
[ConnectionStringName(NotificationDefinitionStoreDbProperties.ConnectionStringName)]
public class NotificationDefinitionStoreMongoDbContext : AbpMongoDbContext, INotificationDefinitionStoreMongoDbContext
{
    public IMongoCollection<NotificationGroupDefinitionRecord> NotificationDefinitionGroups =>
        Collection<NotificationGroupDefinitionRecord>();

    public IMongoCollection<NotificationDefinitionRecord> NotificationDefinitions =>
        Collection<NotificationDefinitionRecord>();

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);

        modelBuilder.ConfigureNotificationDefinitionStore();
    }
}
