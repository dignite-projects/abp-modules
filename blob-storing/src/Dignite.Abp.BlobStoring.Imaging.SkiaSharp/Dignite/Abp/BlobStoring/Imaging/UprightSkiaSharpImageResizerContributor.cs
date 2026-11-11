using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Imaging;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// ABP's SkiaSharp resizer, with the picture turned upright by its EXIF orientation before it is resized.
/// <para>
/// A camera writes a portrait photo sideways and records the turn in its EXIF orientation. ABP 10.7.0's
/// <see cref="SkiaSharpImageResizerContributor"/> decodes with <c>SKBitmap.Decode</c>, which ignores the orientation, and
/// encodes the resized picture without EXIF, so a photo shrunk to fit is stored on its side. This decodes the same way,
/// applies the orientation (<see cref="ImageOrientation"/>), then resizes with ABP's own
/// <see cref="SkiaSharpImageResizerContributor.ApplyResize"/>, so every <see cref="ImageResizeMode"/> means what it means
/// there, applied to the upright picture. Encoding is ABP's too, at <see cref="SkiaSharpResizerOptions.Quality"/>. A
/// picture without an orientation is decoded, resized and encoded as ABP does.
/// </para>
/// <para>
/// <see cref="AbpBlobStoringImagingSkiaSharpModule"/> removes ABP's contributor from the
/// <see cref="IImageResizerContributor"/> registrations, so <see cref="IImageResizer"/> runs this one in its place - for
/// every caller in the application, not only the BLOB pipeline. ABP fixed the same defect on its <c>rel-10.7</c> branch
/// after 10.7.1 (it decodes upright in the resizer and the compressor); once this repository is on a release that has
/// it, this class and the removal can go.
/// </para>
/// </summary>
[ExposeServices(typeof(IImageResizerContributor), typeof(SkiaSharpImageResizerContributor), IncludeSelf = true)]
public class UprightSkiaSharpImageResizerContributor : SkiaSharpImageResizerContributor
{
    public UprightSkiaSharpImageResizerContributor(IOptions<SkiaSharpResizerOptions> options)
        : base(options)
    {
    }

    public override async Task<ImageResizeResult<Stream>> TryResizeAsync(
        Stream stream,
        ImageResizeArgs resizeArgs,
        string? mimeType = null,
        CancellationToken cancellationToken = default)
    {
        if (!mimeType.IsNullOrWhiteSpace() && !CanResize(mimeType))
        {
            return new ImageResizeResult<Stream>(stream, ImageProcessState.Unsupported);
        }

        var position = stream.CanSeek ? stream.Position : 0;
        using var source = new MemoryStream();
        await stream.CopyToAsync(source, cancellationToken);
        if (stream.CanSeek)
        {
            stream.Position = position;
        }

        using var data = SKData.CreateCopy(source.GetBuffer().AsSpan(0, (int)source.Length));
        using var codec = SKCodec.Create(data);
        if (codec == null || !CanEncodeFormat(codec.EncodedFormat))
        {
            return new ImageResizeResult<Stream>(stream, ImageProcessState.Unsupported);
        }

        var origin = codec.EncodedOrigin;
        using var decoded = Decode(codec, origin);
        if (decoded == null)
        {
            return new ImageResizeResult<Stream>(stream, ImageProcessState.Unsupported);
        }

        var upright = ImageOrientation.Orient(decoded, origin);
        try
        {
            using var resized = ApplyResize(upright, resizeArgs);
            using var image = SKImage.FromBitmap(resized);

            var result = new MemoryStream();
            try
            {
                using var encoded = image.Encode(codec.EncodedFormat, Options.Quality);
                encoded.SaveTo(result);
                result.Position = 0;
                return new ImageResizeResult<Stream>(result, ImageProcessState.Done);
            }
            catch
            {
                result.Dispose();
                throw;
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
    /// The picture's pixels: as ABP decodes them when the picture is upright, and as 32-bit pixels, which a canvas
    /// turns, when it is not (a 16-bit or greyscale PNG decodes to pixels a canvas does not draw into).
    /// </summary>
    protected virtual SKBitmap? Decode(SKCodec codec, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return SKBitmap.Decode(codec);
        }

        var info = codec.Info
            .WithColorType(SKImageInfo.PlatformColorType)
            .WithAlphaType(codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul)
            .WithColorSpace(null);
        return SKBitmap.Decode(codec, info);
    }
}
