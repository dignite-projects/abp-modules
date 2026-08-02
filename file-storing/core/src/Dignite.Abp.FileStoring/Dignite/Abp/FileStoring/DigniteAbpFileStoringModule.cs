using Dignite.Abp.FileStoring.Localization;
using Volo.Abp.BlobStoring;
using Volo.Abp.Localization;
using Volo.Abp.Localization.ExceptionHandling;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Abp.FileStoring;

[DependsOn(typeof(AbpBlobStoringModule))]
public class DigniteAbpFileStoringModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<DigniteAbpFileStoringModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<FileStoringResource>("en")
                .AddVirtualJson("/Dignite/Abp/FileStoring/Localization/Resources");
        });

        Configure<AbpExceptionLocalizationOptions>(options =>
        {
            options.MapCodeNamespace("Dignite.Abp.File", typeof(FileStoringResource));
        });
    }
}
