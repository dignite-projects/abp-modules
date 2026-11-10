using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Drive;

[DependsOn(
    typeof(AbpVirtualFileSystemModule)
    )]
public class DriveInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<DriveInstallerModule>();
        });
    }
}
