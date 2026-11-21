using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Imaging;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Stores a PNG as lossless WebP, transparency and all, turned upright and fitted into the container's box in one encode
/// (<see cref="WebpContributorBase"/>): a PNG that already fits keeps every visible pixel. Add it with
/// <see cref="SkiaSharpImagingBlobContainerConfigurationExtensions.StorePngAsWebp"/>, after
/// <c>AddAllowedContentTypesContributor</c> and before any contributor that changes the bytes -
/// <c>AddImageMetadataStripContributor</c>, <c>AddImageResizeContributor</c> - because its rule reads the length.
/// <para>
/// A PNG is stored as it is when it is smaller than <see cref="PngToWebpContributorConfiguration.MinSizeInBytes"/>, has
/// 16 bits per channel (WebP has 8), is an APNG (a still WebP would keep one frame), would not fit WebP's 16383 pixels on a
/// side once fitted into the box, or its header gives no size.
/// </para>
/// </summary>
public class PngToWebpContributor : WebpContributorBase, ITransientDependency
{
    /// <summary>
    /// The effort of the lossless encoder, 0-100 (in lossless mode the quality setting is how hard libwebp tries, not how
    /// much it keeps): 75, the default of libwebp's own <c>cwebp</c>.
    /// </summary>
    public const float DefaultLosslessEffort = 75;

    public PngToWebpContributor(IMimeTypeDetector mimeTypeDetector, IOptions<SkiaSharpResizerOptions> resizerOptions)
        : base(mimeTypeDetector, resizerOptions)
    {
    }

    protected override string SourceContentType => "image/png";

    protected override void ValidateConfiguration(BlobContainerConfiguration configuration)
    {
        configuration.GetPngToWebpContributorConfiguration().Validate();
    }

    /// <summary>
    /// Eight bits per channel in the platform's 32-bit layout, which holds every PNG this contributor converts exactly;
    /// transparency unpremultiplied, as the PNG stores it, so a semi-transparent pixel keeps its colour. A picture that is
    /// turned upright is drawn on a canvas, which needs premultiplied pixels.
    /// </summary>
    protected override SKImageInfo GetDecodeInfo(SKCodec codec)
    {
        var alphaType = codec.Info.AlphaType == SKAlphaType.Opaque
            ? SKAlphaType.Opaque
            : codec.EncodedOrigin == SKEncodedOrigin.TopLeft ? SKAlphaType.Unpremul : SKAlphaType.Premul;

        return codec.Info
            .WithColorType(SKImageInfo.PlatformColorType)
            .WithAlphaType(alphaType);
    }

    protected override SKWebpEncoderOptions GetEncoderOptions(BlobContainerConfiguration configuration)
    {
        return new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, DefaultLosslessEffort);
    }
}
