using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.MongoDB;

namespace Dignite.NotificationCenter.MongoDB;

[DependsOn(
    typeof(AbpNotificationCenterDomainModule),
    typeof(AbpMongoDbModule)
    )]
public class AbpNotificationCenterMongoDbModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMongoDbContext<NotificationCenterMongoDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });
    }
}
