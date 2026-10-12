using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.EntityFrameworkCore;

/// <summary>
/// EF Core repositories of the definition store, after ABP's <c>AbpPermissionManagementEntityFrameworkCoreModule</c>.
/// </summary>
[DependsOn(
    typeof(AbpNotificationsDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
    )]
public class AbpNotificationsEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<NotificationDefinitionStoreDbContext>(options =>
        {
            options.AddDefaultRepositories<INotificationDefinitionStoreDbContext>();

            options.AddRepository<NotificationGroupDefinitionRecord, EfCoreNotificationGroupDefinitionRecordRepository>();
            options.AddRepository<NotificationDefinitionRecord, EfCoreNotificationDefinitionRecordRepository>();
        });
    }
}
