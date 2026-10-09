using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.Notifications.DefinitionStore.EntityFrameworkCore;

/// <summary>
/// The definition store's tables. Definitions are host-level data, so the context ignores multi-tenancy (as ABP's
/// <c>IFeatureManagementDbContext</c>) and always uses the host's <see cref="NotificationDefinitionStoreDbProperties.ConnectionStringName"/>.
/// </summary>
[IgnoreMultiTenancy]
[ConnectionStringName(NotificationDefinitionStoreDbProperties.ConnectionStringName)]
public interface INotificationDefinitionStoreDbContext : IEfCoreDbContext
{
    DbSet<NotificationGroupDefinitionRecord> NotificationDefinitionGroups { get; }

    DbSet<NotificationDefinitionRecord> NotificationDefinitions { get; }
}
