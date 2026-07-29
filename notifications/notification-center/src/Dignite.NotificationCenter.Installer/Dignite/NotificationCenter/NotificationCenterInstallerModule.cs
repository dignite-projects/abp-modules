using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.NotificationCenter;

[DependsOn(typeof(AbpVirtualFileSystemModule))]
public class NotificationCenterInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<NotificationCenterInstallerModule>();
        });
    }
}
