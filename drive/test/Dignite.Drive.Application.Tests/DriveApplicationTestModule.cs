using Volo.Abp.Modularity;

namespace Dignite.Drive;

[DependsOn(
    typeof(DriveApplicationModule),
    typeof(DriveDomainTestModule)
    )]
public class DriveApplicationTestModule : AbpModule
{

}
