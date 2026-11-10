using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Dignite.Drive;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(DriveDomainSharedModule)
)]
public class DriveDomainModule : AbpModule
{

}
