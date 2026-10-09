using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.Notifications.MongoDB;

/// <summary>
/// MongoDB repositories of the definition store, after ABP's <c>AbpPermissionManagementMongoDbModule</c> — the
/// MongoDB counterpart of <c>AbpNotificationsEntityFrameworkCoreModule</c>.
/// </summary>
[DependsOn(
    typeof(AbpNotificationsDomainModule),
    typeof(AbpMongoDbModule)
    )]
public class AbpNotificationsMongoDbModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMongoDbContext<NotificationDefinitionStoreMongoDbContext>(options =>
        {
            options.AddDefaultRepositories<INotificationDefinitionStoreMongoDbContext>();

            options.AddRepository<NotificationGroupDefinitionRecord, MongoNotificationGroupDefinitionRecordRepository>();
            options.AddRepository<NotificationDefinitionRecord, MongoNotificationDefinitionRecordRepository>();
        });
    }
}
