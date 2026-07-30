using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Abp.FlexFields;

[DependsOn(typeof(AbpVirtualFileSystemModule))]
public class FlexFieldsInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<FlexFieldsInstallerModule>();
        });
    }
}
