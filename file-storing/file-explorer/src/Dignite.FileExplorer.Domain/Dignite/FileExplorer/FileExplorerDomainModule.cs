using Dignite.Abp.FileStoring;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Dignite.FileExplorer;

[DependsOn(
    typeof(DigniteAbpFileStoringModule),
    typeof(AbpDddDomainModule),
    typeof(FileExplorerDomainSharedModule)
)]
public class FileExplorerDomainModule : AbpModule
{
}
