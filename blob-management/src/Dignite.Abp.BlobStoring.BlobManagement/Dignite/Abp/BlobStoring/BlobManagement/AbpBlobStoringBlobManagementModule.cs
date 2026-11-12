using Dignite.Abp.BlobManagement;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobStoring.BlobManagement;

[DependsOn(
    typeof(AbpBlobStoringModule),
    typeof(BlobManagementApplicationContractsModule)
    )]
public class AbpBlobStoringBlobManagementModule : AbpModule
{
}
