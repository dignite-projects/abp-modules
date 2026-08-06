using Dignite.Abp.FlexFields.CKEditor.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Abp.FlexFields.CKEditor;

[DependsOn(
    typeof(FlexFieldsAbstractionsModule)
    )]
public class FlexFieldsCKEditorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<FlexFieldsCKEditorModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<FlexFieldsCKEditorResource>("en")
                .AddVirtualJson("/Dignite/Abp/FlexFields/CKEditor/Localization/Resources");
        });
    }
}
