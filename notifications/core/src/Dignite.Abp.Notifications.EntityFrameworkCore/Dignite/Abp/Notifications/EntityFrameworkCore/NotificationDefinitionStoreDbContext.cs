using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.Notifications.EntityFrameworkCore;

/// <summary>
/// The definition store's own DbContext, used at runtime. The host's migration DbContext includes the same tables with
/// <see cref="NotificationDefinitionStoreDbContextModelCreatingExtensions.ConfigureNotificationDefinitionStore"/>; this
/// package ships no migrations.
/// </summary>
[IgnoreMultiTenancy]
[ConnectionStringName(NotificationDefinitionStoreDbProperties.ConnectionStringName)]
public class NotificationDefinitionStoreDbContext :
    AbpDbContext<NotificationDefinitionStoreDbContext>,
    INotificationDefinitionStoreDbContext
{
    public DbSet<NotificationGroupDefinitionRecord> NotificationDefinitionGroups { get; set; } = default!;

    public DbSet<NotificationDefinitionRecord> NotificationDefinitions { get; set; } = default!;

    public NotificationDefinitionStoreDbContext(DbContextOptions<NotificationDefinitionStoreDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureNotificationDefinitionStore();
    }
}
