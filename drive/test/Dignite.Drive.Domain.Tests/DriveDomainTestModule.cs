using Volo.Abp.Modularity;

namespace Dignite.Drive;

[DependsOn(
    typeof(DriveDomainModule),
    typeof(DriveTestBaseModule)
)]
public class DriveDomainTestModule : AbpModule
{

}
