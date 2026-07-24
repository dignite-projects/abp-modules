using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter.EntityFrameworkCore;

[DependsOn(
    typeof(NotificationCenterDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
    )]
public class NotificationCenterEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<NotificationCenterDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });
    }
}
