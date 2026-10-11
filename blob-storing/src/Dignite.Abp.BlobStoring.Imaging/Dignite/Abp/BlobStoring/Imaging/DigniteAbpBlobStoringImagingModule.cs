using Dignite.Abp.BlobStoring.Imaging.Localization;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.Imaging;
using Volo.Abp.Localization;
using Volo.Abp.Localization.ExceptionHandling;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Image resize and compress <see cref="Volo.Abp.BlobStoring.IBlobPipelineContributor"/>s on ABP's provider-agnostic
/// imaging abstractions. This module references no image library: the application also depends on one of ABP's
/// imaging provider modules (<c>AbpImagingSkiaSharpModule</c>, <c>AbpImagingImageSharpModule</c> or
/// <c>AbpImagingMagickNetModule</c>). Without one, every image is unsupported and stored as it is.
/// </summary>
[DependsOn(
    typeof(DigniteAbpBlobStoringPipelineModule),
    typeof(AbpImagingAbstractionsModule)
)]
public class DigniteAbpBlobStoringImagingModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<DigniteAbpBlobStoringImagingModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<BlobStoringImagingResource>("en")
                .AddVirtualJson("/Dignite/Abp/BlobStoring/Imaging/Localization/Resources");
        });

        Configure<AbpExceptionLocalizationOptions>(options =>
        {
            // A namespace of its own: ABP maps a code namespace to exactly one resource, and
            // "Dignite.Abp.BlobStoring" already belongs to the Pipeline package's resource.
            options.MapCodeNamespace(BlobStoringImagingErrorCodes.Namespace, typeof(BlobStoringImagingResource));
        });
    }
}
