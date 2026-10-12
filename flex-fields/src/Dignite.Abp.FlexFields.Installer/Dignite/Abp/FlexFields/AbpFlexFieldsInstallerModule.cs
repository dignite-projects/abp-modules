using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Abp.FlexFields;

[DependsOn(typeof(AbpVirtualFileSystemModule))]
public class AbpFlexFieldsInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<AbpFlexFieldsInstallerModule>();
        });
    }
}
