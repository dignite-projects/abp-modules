using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Imaging;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Stores every JPEG as lossy WebP, at <see cref="JpegToWebpContributorConfiguration.Quality"/>: turned upright by its EXIF
/// orientation (<see cref="ImageOrientation"/>) and, when the container shrinks pictures into a box
/// (<see cref="ImageResizeContributor"/> with <see cref="ImageResizeMode.Max"/>), fitted into that box in the same
/// decode, so the photo is encoded once. Any other content - a PNG, a GIF, a WebP - passes through to the contributors
/// after it. Add it with <see cref="SkiaSharpImagingBlobContainerConfigurationExtensions.StoreJpegAsWebp"/>, after
/// <c>AddImageMetadataStripContributor</c> (which keeps the EXIF orientation) and before <c>AddImageResizeContributor</c>.
/// <para>
/// Every JPEG is converted, whatever the size of the WebP, so what the container stores can be known before the save.
/// </para>
/// <para>
/// It derives from <see cref="ImageContributorBase"/>, so the content is capped, its type detected from the bytes, and the
/// container's decode guard bounds the picture before anything decodes it; a JPEG SkiaSharp cannot decode fails with
/// <see cref="BlobStoringImagingErrorCodes.ImageProcessingFailed"/>. A one-way transform: the stored WebP is read as it
/// is, and the contributor can be added to a container that already has blobs (the JPEGs stored before stay JPEGs).
/// The shrink samples with <see cref="SkiaSharpResizerOptions.SKSamplingOptions"/>, as ABP's resizer does.
/// </para>
/// </summary>
public class JpegToWebpContributor : ImageContributorBase, ITransientDependency
{
    protected SkiaSharpResizerOptions ResizerOptions { get; }

    public JpegToWebpContributor(IMimeTypeDetector mimeTypeDetector, IOptions<SkiaSharpResizerOptions> resizerOptions)
        : base(mimeTypeDetector)
    {
        ResizerOptions = resizerOptions.Value;
    }

    protected override void ValidateConfiguration(BlobContainerConfiguration configuration)
    {
        configuration.GetJpegToWebpContributorConfiguration().Validate();
    }

    protected override async Task<ImageProcessResult<Stream>?> ProcessAsync(
        BlobPipelineContext context,
        Stream stream,
        string mimeType,
        (int Width, int Height)? dimensions,
        CancellationToken cancellationToken)
    {
        if (!IsJpeg(mimeType))
        {
            return null;
        }

        var quality = context.Configuration.GetJpegToWebpContributorConfiguration().Quality;
        var box = GetBox(context.Configuration);

        using var data = await ReadAsync(stream, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        using var codec = SKCodec.Create(data) ?? throw new InvalidOperationException("Not a JPEG SkiaSharp can read.");
        var info = codec.Info
            .WithColorType(SKImageInfo.PlatformColorType)
            .WithAlphaType(SKAlphaType.Opaque);
        using var decoded = SKBitmap.Decode(codec, info) ?? throw new InvalidOperationException("The JPEG's pixels could not be decoded.");

        var upright = ImageOrientation.Orient(decoded, codec.EncodedOrigin);
        try
        {
            var fitted = box == null ? upright : Fit(upright, box.Value.MaxWidth, box.Value.MaxHeight);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var pixmap = fitted.PeekPixels() ?? throw new InvalidOperationException("The picture's pixels are not readable.");
                using var encoded = pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, quality))
                                    ?? throw new InvalidOperationException("The picture could not be encoded as WebP.");
                return new ImageCompressResult<Stream>(new MemoryStream(encoded.ToArray(), writable: false), ImageProcessState.Done);
            }
            finally
            {
                if (!ReferenceEquals(fitted, upright))
                {
                    fitted.Dispose();
                }
            }
        }
        finally
        {
            if (!ReferenceEquals(upright, decoded))
            {
                upright.Dispose();
            }
        }
    }

    protected virtual bool IsJpeg(string mimeType)
    {
        return string.Equals(mimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The box the container shrinks pictures into, which the converted JPEG is fitted into in the same decode: the
    /// container's <see cref="ImageResizeContributor"/> box when it fits pictures inside it
    /// (<see cref="ImageResizeMode.Max"/>, its default), else <c>null</c> - with another mode the WebP is stored at its
    /// upright size and the resize contributor after this one applies its mode to it.
    /// </summary>
    protected virtual (int MaxWidth, int MaxHeight)? GetBox(BlobContainerConfiguration configuration)
    {
        if (!configuration.PipelineContributors.Contains(typeof(ImageResizeContributor)))
        {
            return null;
        }

        var resize = configuration.GetImageResizeContributorConfiguration();
        if (resize.Mode != ImageResizeMode.Max || resize.MaxWidth <= 0 && resize.MaxHeight <= 0)
        {
            return null;
        }

        return (resize.MaxWidth, resize.MaxHeight);
    }

    /// <summary>
    /// <paramref name="picture"/> shrunk to fit inside <paramref name="maxWidth"/> x <paramref name="maxHeight"/> (a side
    /// of <c>0</c> is unconstrained), keeping its aspect ratio, as <see cref="ImageResizeMode.Max"/>; never enlarged.
    /// </summary>
    protected virtual SKBitmap Fit(SKBitmap picture, int maxWidth, int maxHeight)
    {
        var scale = 1.0;
        if (maxWidth > 0 && picture.Width > maxWidth)
        {
            scale = Math.Min(scale, (double)maxWidth / picture.Width);
        }

        if (maxHeight > 0 && picture.Height > maxHeight)
        {
            scale = Math.Min(scale, (double)maxHeight / picture.Height);
        }

        if (scale >= 1.0)
        {
            return picture;
        }

        var width = Math.Max(1, (int)Math.Round(picture.Width * scale));
        var height = Math.Max(1, (int)Math.Round(picture.Height * scale));
        return picture.Resize(picture.Info.WithSize(width, height), ResizerOptions.SKSamplingOptions)
               ?? throw new InvalidOperationException("The picture could not be resized.");
    }

    /// <summary>The content of <paramref name="stream"/> (seekable, at position 0), which is left open.</summary>
    protected virtual async Task<SKData> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is MemoryStream memory && memory.TryGetBuffer(out var buffer))
        {
            return SKData.CreateCopy(buffer.AsSpan((int)memory.Position, (int)(memory.Length - memory.Position)));
        }

        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken);
        return SKData.CreateCopy(copy.GetBuffer().AsSpan(0, (int)copy.Length));
    }
}
