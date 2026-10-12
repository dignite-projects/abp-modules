using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter.EntityFrameworkCore;

[DependsOn(
    typeof(AbpNotificationCenterDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
    )]
public class AbpNotificationCenterEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<NotificationCenterDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });
    }
}
