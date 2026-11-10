using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Dignite.Drive;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(AbpBlobStoringPipelineModule),
    typeof(DriveDomainSharedModule)
)]
public class DriveDomainModule : AbpModule
{

}
