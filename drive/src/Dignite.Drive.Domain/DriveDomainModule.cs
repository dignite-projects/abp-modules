using Volo.Abp.Domain;
using Volo.Abp.Modularity;
using Volo.Abp.Commercial.SuiteTemplates;

namespace Dignite.Drive;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(VoloAbpCommercialSuiteTemplatesModule),
    typeof(DriveDomainSharedModule)
)]
public class DriveDomainModule : AbpModule
{

}
