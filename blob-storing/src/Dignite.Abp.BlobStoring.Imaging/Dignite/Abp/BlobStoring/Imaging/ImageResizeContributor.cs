using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Imaging;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Shrinks images that exceed <see cref="ImageResizeContributorConfiguration.MaxWidth"/> /
/// <see cref="ImageResizeContributorConfiguration.MaxHeight"/> with ABP's <see cref="IImageResizer"/>, and rejects
/// images smaller than <see cref="ImageResizeContributorConfiguration.MinWidth"/> /
/// <see cref="ImageResizeContributorConfiguration.MinHeight"/> with
/// <see cref="BlobStoringImagingErrorCodes.ImageTooSmall"/>. Configure it with
/// <see cref="ImagingBlobContainerConfigurationExtensions.AddImageResizeContributor"/>.
/// <para>
/// The image is only resized when the dimensions read from its header exceed a constrained dimension, so an image
/// that already fits is stored byte for byte and never upscaled. When the header gives no dimensions, the resizer is
/// called anyway, and what happens to an image that already fits is up to the provider and the mode (SkiaSharp's
/// <see cref="ImageResizeMode.Max"/>, for one, scales it up to the box). Every format ABP's providers resize (JPEG,
/// PNG, GIF, BMP, TIFF, WebP) has a header this package reads, so that only happens with an unusual header.
/// </para>
/// <para>
/// Content that is not an image passes through, and so does an image the provider does not support
/// (<see cref="ImageProcessState.Unsupported"/>; SkiaSharp, for example, resizes JPEG, PNG and WebP only). See
/// <see cref="ImageContributorBase"/> for the shared flow and the decode guard.
/// </para>
/// </summary>
public class ImageResizeContributor : ImageContributorBase, ITransientDependency
{
    protected IImageResizer ImageResizer { get; }

    public ImageResizeContributor(IMimeTypeDetector mimeTypeDetector, IImageResizer imageResizer)
        : base(mimeTypeDetector)
    {
        ImageResizer = imageResizer;
    }

    protected override void ValidateConfiguration(BlobContainerConfiguration configuration)
    {
        configuration.GetImageResizeContributorConfiguration().Validate();
    }

    protected override async Task<ImageProcessResult<Stream>?> ProcessAsync(
        BlobPipelineContext context,
        Stream stream,
        string mimeType,
        (int Width, int Height)? dimensions,
        CancellationToken cancellationToken)
    {
        var configuration = context.Configuration.GetImageResizeContributorConfiguration();

        if (dimensions != null)
        {
            CheckMinimumSize(dimensions.Value, configuration);

            if (!ExceedsMaximumSize(dimensions.Value, configuration))
            {
                return null;
            }
        }

        return await ImageResizer.ResizeAsync(stream, CreateResizeArgs(configuration), mimeType, cancellationToken);
    }

    /// <summary>
    /// Fails with <see cref="BlobStoringImagingErrorCodes.ImageTooSmall"/> when the image is smaller than a configured
    /// minimum.
    /// </summary>
    protected virtual void CheckMinimumSize((int Width, int Height) dimensions, ImageResizeContributorConfiguration configuration)
    {
        if (configuration.MinWidth > 0 && dimensions.Width < configuration.MinWidth ||
            configuration.MinHeight > 0 && dimensions.Height < configuration.MinHeight)
        {
            throw new BusinessException(
                    code: BlobStoringImagingErrorCodes.ImageTooSmall,
                    message: "The image is smaller than the minimum size.",
                    details: $"The image is {dimensions.Width}x{dimensions.Height}; the minimum is " +
                             $"{configuration.MinWidth}x{configuration.MinHeight} (0 means no minimum).")
                .WithData("Width", dimensions.Width)
                .WithData("Height", dimensions.Height)
                .WithData("MinWidth", configuration.MinWidth)
                .WithData("MinHeight", configuration.MinHeight);
        }
    }

    /// <summary>
    /// Whether the image exceeds a constrained dimension (0 leaves a dimension unconstrained).
    /// </summary>
    protected virtual bool ExceedsMaximumSize((int Width, int Height) dimensions, ImageResizeContributorConfiguration configuration)
    {
        return configuration.MaxWidth > 0 && dimensions.Width > configuration.MaxWidth ||
               configuration.MaxHeight > 0 && dimensions.Height > configuration.MaxHeight;
    }

    protected virtual ImageResizeArgs CreateResizeArgs(ImageResizeContributorConfiguration configuration)
    {
        return new ImageResizeArgs((uint)configuration.MaxWidth, (uint)configuration.MaxHeight, configuration.Mode);
    }
}
