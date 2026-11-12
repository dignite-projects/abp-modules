using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Abp.BlobManagement;

[DependsOn(
    typeof(AbpVirtualFileSystemModule)
    )]
public class BlobManagementInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<BlobManagementInstallerModule>();
        });
    }
}
