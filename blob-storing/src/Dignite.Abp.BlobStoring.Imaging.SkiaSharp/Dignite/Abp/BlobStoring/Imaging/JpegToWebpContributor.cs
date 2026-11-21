using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Imaging;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Stores a JPEG as lossy WebP, at <see cref="JpegToWebpContributorConfiguration.Quality"/>, turned upright and fitted into
/// the container's box in one encode (<see cref="WebpContributorBase"/>). Add it with
/// <see cref="SkiaSharpImagingBlobContainerConfigurationExtensions.StoreJpegAsWebp"/>, after
/// <c>AddAllowedContentTypesContributor</c> and before <c>AddImageResizeContributor</c>.
/// <para>
/// Every JPEG is converted except one that would not fit WebP's 16383 pixels on a side - only possible when the container
/// has no box, or one larger than that - or whose header gives no size: such a JPEG is stored as it is. The rule reads the
/// header only, which <c>AddImageMetadataStripContributor</c> keeps, so the contributor may come before or after that one.
/// </para>
/// </summary>
public class JpegToWebpContributor : WebpContributorBase, ITransientDependency
{
    public JpegToWebpContributor(IMimeTypeDetector mimeTypeDetector, IOptions<SkiaSharpResizerOptions> resizerOptions)
        : base(mimeTypeDetector, resizerOptions)
    {
    }

    protected override string SourceContentType => "image/jpeg";

    protected override void ValidateConfiguration(BlobContainerConfiguration configuration)
    {
        configuration.GetJpegToWebpContributorConfiguration().Validate();
    }

    /// <summary>A JPEG has no transparency: opaque pixels in the platform's 32-bit layout.</summary>
    protected override SKImageInfo GetDecodeInfo(SKCodec codec)
    {
        return codec.Info
            .WithColorType(SKImageInfo.PlatformColorType)
            .WithAlphaType(SKAlphaType.Opaque);
    }

    protected override SKWebpEncoderOptions GetEncoderOptions(BlobContainerConfiguration configuration)
    {
        return new SKWebpEncoderOptions(
            SKWebpEncoderCompression.Lossy,
            configuration.GetJpegToWebpContributorConfiguration().Quality);
    }
}
