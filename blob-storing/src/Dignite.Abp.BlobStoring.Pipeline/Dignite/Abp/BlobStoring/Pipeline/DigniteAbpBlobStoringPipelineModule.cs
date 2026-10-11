using Dignite.Abp.BlobStoring.Pipeline.Localization;
using FileSignatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.BlobStoring;
using Volo.Abp.Localization;
using Volo.Abp.Localization.ExceptionHandling;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Abp.BlobStoring.Pipeline;

[DependsOn(typeof(AbpBlobStoringModule))]
public class DigniteAbpBlobStoringPipelineModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // The signature engine behind MimeTypeDetector. Registered as a replaceable singleton (it is
        // stateless once its format list is built), so an application can register its own inspector
        // with additional FileFormat types.
        context.Services.TryAddSingleton<IFileFormatInspector>(
            _ => new FileFormatInspector(FileFormatLocator.GetFormats()));

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<DigniteAbpBlobStoringPipelineModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<BlobStoringPipelineResource>("en")
                .AddVirtualJson("/Dignite/Abp/BlobStoring/Pipeline/Localization/Resources");
        });

        Configure<AbpExceptionLocalizationOptions>(options =>
        {
            options.MapCodeNamespace(BlobStoringPipelineErrorCodes.Namespace, typeof(BlobStoringPipelineResource));
        });
    }
}
