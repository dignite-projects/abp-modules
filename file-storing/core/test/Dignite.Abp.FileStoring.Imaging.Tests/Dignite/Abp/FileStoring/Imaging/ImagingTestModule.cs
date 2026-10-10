using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;

namespace Dignite.Abp.FileStoring.Imaging;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(DigniteAbpFileStoringImagingModule)
    )]
public class ImagingTestModule : AbpModule
{
    public const string PhotosContainer = "photos";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.ConfigureDefault(container => container.ProviderType = typeof(InMemoryBlobProvider));

            options.Containers.Configure(PhotosContainer, container =>
            {
                container.ProviderType = typeof(InMemoryBlobProvider);
                container.AddFileSizeLimitHandler(handler => handler.MaxFileSize = 5);
                container.AddFileTypeCheckHandler(handler => handler.AllowedFileTypeNames = [".jpg", ".png"]);
                container.AddImageResizeHandler(handler =>
                {
                    handler.ImageWidth = 200;
                    handler.ImageHeight = 200;
                });
            });
        });
    }
}
