using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Volo.Abp.Imaging;
using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The SkiaSharp image contributors (<see cref="JpegToWebpContributor"/>) on ABP's SkiaSharp imaging provider, which this
/// module brings. It also changes how ABP's SkiaSharp resizer behaves for the whole application:
/// <list type="bullet">
/// <item>ABP's <see cref="SkiaSharpImageResizerContributor"/> is replaced by
/// <see cref="UprightSkiaSharpImageResizerContributor"/>, which turns a photo upright by its EXIF orientation before it
/// resizes it. <see cref="IImageResizer"/> asks every contributor in turn, so ABP's is removed rather than outranked.</item>
/// <item><see cref="SkiaSharpResizerOptions"/> default to linear filtering between linear-blended mipmap levels and
/// quality 85, instead of ABP's nearest-neighbour sampling and 75. A host's own
/// <c>Configure&lt;SkiaSharpResizerOptions&gt;</c> runs after this module's and wins.</item>
/// </list>
/// On Linux the host also references <c>SkiaSharp.NativeAssets.Linux</c>: SkiaSharp brings the Windows and macOS native
/// libraries only.
/// </summary>
[DependsOn(
    typeof(AbpBlobStoringImagingModule),
    typeof(AbpImagingSkiaSharpModule)
)]
public class AbpBlobStoringImagingSkiaSharpModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Nearest-neighbour (ABP's default, SKSamplingOptions.Default) keeps one source pixel per target pixel and drops
        // the rest, so a shrunk photo turns fine detail into jagged noise and moire. Linear between linear-blended mipmap
        // levels averages instead: measured against an exact area-average downscale it scored 25-46 dB PSNR where the
        // default scored 9-33 dB (a zone plate, a noisy photo-like picture and a screenshot, shrunk to 1920/800/400 px).
        // Quality 85 rather than 75 for the re-encode of a picture shrunk to fit.
        Configure<SkiaSharpResizerOptions>(options =>
        {
            options.SKSamplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            options.Quality = 85;
        });

        // UprightSkiaSharpImageResizerContributor registers itself (ExposeServices); ABP's, registered by its own module
        // before this one, has to go.
        context.Services.RemoveAll(descriptor =>
            descriptor.ServiceType == typeof(IImageResizerContributor) &&
            descriptor.ImplementationType == typeof(SkiaSharpImageResizerContributor));
    }
}
