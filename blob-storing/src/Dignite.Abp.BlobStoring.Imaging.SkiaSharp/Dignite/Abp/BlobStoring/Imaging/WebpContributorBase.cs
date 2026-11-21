using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Imaging;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Stores pictures of one type (<see cref="SourceContentType"/>) as WebP: decoded once, turned upright by their EXIF
/// orientation (<see cref="ImageOrientation"/>) and, when the container shrinks pictures into a box
/// (<see cref="ImageResizeContributor"/> with <see cref="ImageResizeMode.Max"/>), fitted into that box in the same decode,
/// so the picture is encoded once. The derived contributor says how it is decoded and encoded.
/// <para>
/// Which pictures are converted is the container's declaration: a picture of the type is converted when
/// <c>GetStoredContentType</c> says the container stores it as <c>image/webp</c>. The extension that adds the contributor
/// declares the rule (<c>StoreJpegAsWebp</c>, <c>StorePngAsWebp</c>), so a caller that names its blobs before the save reads
/// the same answer; a picture the rule leaves out passes through to the contributors after this one, as does anything of
/// another type.
/// </para>
/// <para>
/// It derives from <see cref="ImageContributorBase"/>, so the content is capped, its type detected from the bytes, and the
/// container's decode guard bounds the picture before anything decodes it; a picture SkiaSharp cannot decode fails with
/// <see cref="BlobStoringImagingErrorCodes.ImageProcessingFailed"/>. A one-way transform: the stored WebP is read as it
/// is, and the contributor can be added to a container that already has blobs (those stored before stay as they are).
/// The shrink samples with <see cref="SkiaSharpResizerOptions.SKSamplingOptions"/>, as ABP's resizer does.
/// </para>
/// </summary>
public abstract class WebpContributorBase : ImageContributorBase
{
    protected SkiaSharpResizerOptions ResizerOptions { get; }

    protected WebpContributorBase(IMimeTypeDetector mimeTypeDetector, IOptions<SkiaSharpResizerOptions> resizerOptions)
        : base(mimeTypeDetector)
    {
        ResizerOptions = resizerOptions.Value;
    }

    /// <summary>The type this contributor converts, as <see cref="IMimeTypeDetector"/> detects it.</summary>
    protected abstract string SourceContentType { get; }

    /// <summary>The pixels <paramref name="codec"/>'s picture is decoded into.</summary>
    protected abstract SKImageInfo GetDecodeInfo(SKCodec codec);

    /// <summary>How the WebP is encoded, for the container <paramref name="configuration"/>.</summary>
    protected abstract SKWebpEncoderOptions GetEncoderOptions(BlobContainerConfiguration configuration);

    protected override async Task<ImageProcessResult<Stream>?> ProcessAsync(
        BlobPipelineContext context,
        Stream stream,
        string mimeType,
        (int Width, int Height)? dimensions,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(mimeType, SourceContentType, StringComparison.OrdinalIgnoreCase) ||
            !IsStoredAsWebp(context.Configuration, mimeType, stream))
        {
            return null;
        }

        var box = WebpConversion.GetBox(context.Configuration);
        var encoderOptions = GetEncoderOptions(context.Configuration);

        using var data = await ReadAsync(stream, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        using var codec = SKCodec.Create(data) ?? throw new InvalidOperationException("Not a picture SkiaSharp can read.");
        using var decoded = SKBitmap.Decode(codec, GetDecodeInfo(codec))
                            ?? throw new InvalidOperationException("The picture's pixels could not be decoded.");

        var upright = ImageOrientation.Orient(decoded, codec.EncodedOrigin);
        try
        {
            var fitted = Fit(upright, box);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var pixmap = fitted.PeekPixels() ?? throw new InvalidOperationException("The picture's pixels are not readable.");
                using var encoded = pixmap.Encode(encoderOptions)
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

    /// <summary>
    /// Whether the container stores this picture as WebP, by its declaration (<c>GetStoredContentType</c>), read from
    /// <paramref name="stream"/> as this contributor receives it; the stream's position is restored.
    /// </summary>
    protected virtual bool IsStoredAsWebp(BlobContainerConfiguration configuration, string mimeType, Stream stream)
    {
        return string.Equals(
            configuration.GetStoredContentType(mimeType, stream),
            WebpConversion.WebpContentType,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <paramref name="picture"/> shrunk to fit inside <paramref name="box"/> (a side of <c>0</c> is unconstrained),
    /// keeping its aspect ratio, as <see cref="ImageResizeMode.Max"/>; never enlarged, and itself without a box. The size
    /// is the one the declaration measured, so what it said fits WebP's limit is what is encoded.
    /// </summary>
    protected virtual SKBitmap Fit(SKBitmap picture, (int MaxWidth, int MaxHeight)? box)
    {
        var (width, height) = WebpConversion.GetFittedSize((picture.Width, picture.Height), box);
        if (width == picture.Width && height == picture.Height)
        {
            return picture;
        }

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
