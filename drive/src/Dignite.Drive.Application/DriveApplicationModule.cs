using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Application;

namespace Dignite.Drive;

[DependsOn(
    typeof(DriveDomainModule),
    typeof(DriveApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule)
    )]
public class DriveApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<DriveApplicationModule>();
    }
}
