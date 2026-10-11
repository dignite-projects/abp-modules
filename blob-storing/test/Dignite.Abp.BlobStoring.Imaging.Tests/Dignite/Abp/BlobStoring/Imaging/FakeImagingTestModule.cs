using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.BlobStoring;
using Volo.Abp.Imaging;
using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The image contributors on scripted fakes of <see cref="IImageResizer"/> and <see cref="IImageCompressor"/>, to
/// drive the results and failures a real provider cannot be made to produce on demand.
/// </summary>
[DependsOn(typeof(BlobStoringImagingTestModule))]
public class FakeImagingTestModule : AbpModule
{
    public const string ResizeContainer = "fake-resize";
    public const string CompressContainer = "fake-compress";

    public const int ResizeMaxWidth = 100;

    public static readonly TimeSpan DecodeTimeout = TimeSpan.FromMilliseconds(200);

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<FakeImageResizer>();
        context.Services.AddSingleton<FakeImageCompressor>();
        context.Services.Replace(ServiceDescriptor.Transient<IImageResizer>(sp => sp.GetRequiredService<FakeImageResizer>()));
        context.Services.Replace(ServiceDescriptor.Transient<IImageCompressor>(sp => sp.GetRequiredService<FakeImageCompressor>()));

        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.Configure(ResizeContainer, container =>
            {
                container.AddImageResizeContributor(c => c.MaxWidth = ResizeMaxWidth);
                container.ConfigureImageDecodeGuard(g => g.DecodeTimeout = DecodeTimeout);
            });

            options.Containers.Configure(CompressContainer, container =>
            {
                container.AddImageCompressContributor();
                container.ConfigureImageDecodeGuard(g => g.DecodeTimeout = DecodeTimeout);
            });
        });
    }
}
