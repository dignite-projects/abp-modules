using Dignite.Abp.FlexFields.FileExplorer.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Abp.FlexFields.FileExplorer;

[DependsOn(
    typeof(FlexFieldsAbstractionsModule)
    )]
public class FlexFieldsFileExplorerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<FlexFieldsFileExplorerModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<FlexFieldsFileExplorerResource>("en")
                .AddVirtualJson("/Dignite/Abp/FlexFields/FileExplorer/Localization/Resources");
        });
    }
}
