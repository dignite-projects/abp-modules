using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.Notifications.MongoDB;

/// <summary>
/// The definition store's collections. Definitions are host-level data, so the context ignores multi-tenancy (as ABP's
/// <c>IFeatureManagementMongoDbContext</c>) and always uses the host's
/// <see cref="NotificationDefinitionStoreDbProperties.ConnectionStringName"/>.
/// </summary>
[IgnoreMultiTenancy]
[ConnectionStringName(NotificationDefinitionStoreDbProperties.ConnectionStringName)]
public interface INotificationDefinitionStoreMongoDbContext : IAbpMongoDbContext
{
    IMongoCollection<NotificationGroupDefinitionRecord> NotificationDefinitionGroups { get; }

    IMongoCollection<NotificationDefinitionRecord> NotificationDefinitions { get; }
}
