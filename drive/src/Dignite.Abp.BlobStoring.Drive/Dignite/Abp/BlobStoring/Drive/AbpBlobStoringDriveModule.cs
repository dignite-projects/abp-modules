using Dignite.Drive;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobStoring.Drive;

[DependsOn(
    typeof(AbpBlobStoringModule),
    typeof(DriveApplicationContractsModule)
    )]
public class AbpBlobStoringDriveModule : AbpModule
{
}
