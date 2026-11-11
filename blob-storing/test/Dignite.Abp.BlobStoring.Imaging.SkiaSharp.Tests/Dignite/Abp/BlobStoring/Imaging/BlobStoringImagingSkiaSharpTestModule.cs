using System.Collections.Generic;
using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Memory;
using Volo.Abp.Imaging;
using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobStoring.Imaging;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpBlobStoringMemoryModule),
    typeof(AbpBlobStoringImagingSkiaSharpModule)
)]
public class BlobStoringImagingSkiaSharpTestModule : AbpModule
{
    public const string WebpContainer = "webp";
    public const string LowQualityWebpContainer = "webp-low-quality";
    public const string ComposedContainer = "composed";
    public const string StretchContainer = "webp-stretch";
    public const string ResizeContainer = "resize";

    public const int LowQuality = 30;
    public const int Box = 256;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // MemoryBlobProvider is transient and keeps its store per instance; share one instance so the tests can read
        // what a container stored straight from the provider.
        context.Services.RemoveAll(descriptor =>
            descriptor.ServiceType == typeof(IBlobProvider) && descriptor.ImplementationType == typeof(MemoryBlobProvider));
        context.Services.Replace(ServiceDescriptor.Singleton<MemoryBlobProvider, MemoryBlobProvider>());
        context.Services.AddSingleton<IBlobProvider>(serviceProvider => serviceProvider.GetRequiredService<MemoryBlobProvider>());

        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.ConfigureDefault(container => container.UseMemory());

            options.Containers.Configure(WebpContainer, container => container.StoreJpegAsWebp());

            options.Containers.Configure(LowQualityWebpContainer, container => container.StoreJpegAsWebp(LowQuality));

            // The order the images preset of a Drive uses.
            options.Containers.Configure(ComposedContainer, container =>
            {
                container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 4 * 1024 * 1024);
                container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["image/*"]);
                container.AddImageMetadataStripContributor();
                container.StoreJpegAsWebp();
                container.AddImageResizeContributor(c =>
                {
                    c.MaxWidth = Box;
                    c.MaxHeight = Box;
                });
            });

            options.Containers.Configure(StretchContainer, container =>
            {
                container.StoreJpegAsWebp();
                container.AddImageResizeContributor(c =>
                {
                    c.MaxWidth = Box;
                    c.MaxHeight = Box;
                    c.Mode = ImageResizeMode.Stretch;
                });
            });

            options.Containers.Configure(ResizeContainer, container =>
                container.AddImageResizeContributor(c =>
                {
                    c.MaxWidth = Box;
                    c.MaxHeight = Box;
                }));
        });
    }
}
