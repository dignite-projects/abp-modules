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

/// <summary>
/// The image contributors on ABP's SkiaSharp imaging provider.
/// </summary>
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpBlobStoringMemoryModule),
    typeof(AbpImagingSkiaSharpModule),
    typeof(DigniteAbpBlobStoringImagingModule)
)]
public class BlobStoringImagingTestModule : AbpModule
{
    public const string ResizeContainer = "resize";
    public const string ResizeWidthContainer = "resize-width";
    public const string MinimumSizeContainer = "minimum-size";
    public const string GuardedContainer = "guarded";
    public const string CompressContainer = "compress";
    public const string ComposedContainer = "composed";

    public const int ResizeMaxSize = 200;
    public const int ResizeMaxWidth = 300;
    public const int MinWidth = 100;
    public const int MinHeight = 80;
    public const int GuardMaxSourceWidth = 500;
    public const int ComposedMaxSize = 256;
    public const long ComposedMaxSizeInBytes = 4 * 1024 * 1024;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // MemoryBlobProvider is transient and keeps its store per instance; share one instance so the tests
        // can read what a container stored straight from the provider.
        context.Services.RemoveAll(descriptor =>
            descriptor.ServiceType == typeof(IBlobProvider) && descriptor.ImplementationType == typeof(MemoryBlobProvider));
        context.Services.Replace(ServiceDescriptor.Singleton<MemoryBlobProvider, MemoryBlobProvider>());
        context.Services.AddSingleton<IBlobProvider>(serviceProvider => serviceProvider.GetRequiredService<MemoryBlobProvider>());

        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.ConfigureDefault(container => container.UseMemory());

            options.Containers.Configure(ResizeContainer, container =>
                container.AddImageResizeContributor(c =>
                {
                    c.MaxWidth = ResizeMaxSize;
                    c.MaxHeight = ResizeMaxSize;
                }));

            options.Containers.Configure(ResizeWidthContainer, container =>
                container.AddImageResizeContributor(c => c.MaxWidth = ResizeMaxWidth));

            options.Containers.Configure(MinimumSizeContainer, container =>
                container.AddImageResizeContributor(c =>
                {
                    c.MinWidth = MinWidth;
                    c.MinHeight = MinHeight;
                    c.MaxWidth = 1000;
                }));

            options.Containers.Configure(GuardedContainer, container =>
            {
                container.AddImageResizeContributor(c => c.MaxWidth = 1000);
                container.ConfigureImageDecodeGuard(g => g.MaxSourceWidth = GuardMaxSourceWidth);
            });

            options.Containers.Configure(CompressContainer, container =>
                container.AddImageCompressContributor());

            options.Containers.Configure(ComposedContainer, container =>
            {
                container.AddMaxSizeContributor(c => c.MaxSizeInBytes = ComposedMaxSizeInBytes);
                container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["image/*"]);
                container.AddImageResizeContributor(c =>
                {
                    c.MaxWidth = ComposedMaxSize;
                    c.MaxHeight = ComposedMaxSize;
                });
                container.AddImageCompressContributor();
            });
        });
    }
}
