using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Dignite.Drive;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(AbpBlobStoringPipelineModule),
    typeof(AbpBlobStoringImagingModule),
    typeof(DriveDomainSharedModule)
)]
public class DriveDomainModule : AbpModule
{

}
