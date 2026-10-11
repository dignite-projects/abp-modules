using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Imaging;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Re-encodes images with ABP's <see cref="IImageCompressor"/>, keeping their format and dimensions. Configure it with
/// <see cref="ImagingBlobContainerConfigurationExtensions.AddImageCompressContributor"/>; the encoder settings are the
/// provider's (for example <c>SkiaSharpCompressOptions.Quality</c>).
/// <para>
/// The compressed image replaces the content only when the provider reports <see cref="ImageProcessState.Done"/>.
/// ABP's providers return <see cref="ImageProcessState.Canceled"/> when the re-encoded image is not smaller than the
/// original, so a <see cref="ImageProcessState.Canceled"/> result keeps the original image instead of failing the save
/// (<see cref="HandleCanceledResult"/>). Content that is not an image passes through, and so does an image the
/// provider does not support (<see cref="ImageProcessState.Unsupported"/>; SkiaSharp and ImageSharp compress JPEG, PNG
/// and WebP). See <see cref="ImageContributorBase"/> for the shared flow and the decode guard.
/// </para>
/// </summary>
public class ImageCompressContributor : ImageContributorBase, ITransientDependency
{
    protected IImageCompressor ImageCompressor { get; }

    public ImageCompressContributor(IMimeTypeDetector mimeTypeDetector, IImageCompressor imageCompressor)
        : base(mimeTypeDetector)
    {
        ImageCompressor = imageCompressor;
    }

    protected override void ValidateConfiguration(BlobContainerConfiguration configuration)
    {
        configuration.GetImageCompressContributorConfiguration().Validate();
    }

    protected override async Task<ImageProcessResult<Stream>?> ProcessAsync(
        BlobPipelineContext context,
        Stream stream,
        string mimeType,
        (int Width, int Height)? dimensions,
        CancellationToken cancellationToken)
    {
        return await ImageCompressor.CompressAsync(stream, mimeType, cancellationToken);
    }

    /// <summary>
    /// Keeps the original image: for ABP's compressors, <see cref="ImageProcessState.Canceled"/> means compressing
    /// would not have made the image smaller.
    /// </summary>
    protected override void HandleCanceledResult(BlobPipelineContext context, Stream stream)
    {
    }
}
