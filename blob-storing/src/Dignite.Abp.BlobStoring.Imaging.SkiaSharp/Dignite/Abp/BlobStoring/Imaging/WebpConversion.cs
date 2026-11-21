using System;
using System.IO;
using Volo.Abp.BlobStoring;
using Volo.Abp.Imaging;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The rules the WebP contributors convert by. <c>StoreJpegAsWebp</c> and <c>StorePngAsWebp</c> declare them as the
/// conditions of their stored types (<c>SetStoredContentType</c>), and the contributors read the declaration, so a caller
/// that names its blobs before the save and the contributor in the save get the same answer. They read the picture's
/// header, its length and the container's configuration - never anything a save computes.
/// </summary>
internal static class WebpConversion
{
    public const string WebpContentType = "image/webp";

    /// <summary>WebP's limit on each side, in pixels (14 bits).</summary>
    public const int MaxDimension = 16383;

    /// <summary>
    /// A JPEG is stored as WebP when its header gives its size and it fits WebP's limit once fitted into the container's
    /// box (<see cref="FitsWebp"/>).
    /// </summary>
    public static bool CanStoreJpegAsWebp(BlobContainerConfiguration configuration, Stream content)
    {
        return FitsWebp(configuration, content);
    }

    /// <summary>
    /// A PNG is stored as WebP unless it is smaller than <see cref="PngToWebpContributorConfiguration.MinSizeInBytes"/>
    /// (too little to save), has 16 bits per channel (WebP has 8), is an APNG (one frame would be kept), or does not fit
    /// WebP's limit once fitted into the container's box.
    /// </summary>
    public static bool CanStorePngAsWebp(BlobContainerConfiguration configuration, Stream content)
    {
        return content.Length >= configuration.GetPngToWebpContributorConfiguration().MinSizeInBytes &&
               ReadPngBitDepth(content) is >= 1 and <= 8 &&
               !ImageHeaderReader.IsAnimated(content) &&
               FitsWebp(configuration, content);
    }

    /// <summary>
    /// The box the container shrinks pictures into, which a converted picture is fitted into in the same decode: the
    /// container's <see cref="ImageResizeContributor"/> box when it fits pictures inside it (<see cref="ImageResizeMode.Max"/>,
    /// its default), else <c>null</c> - with another mode the WebP is stored at its upright size and the resize contributor
    /// after the conversion applies its mode to it.
    /// </summary>
    public static (int MaxWidth, int MaxHeight)? GetBox(BlobContainerConfiguration configuration)
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
    /// <paramref name="size"/> shrunk to fit inside <paramref name="box"/> (a side of <c>0</c> is unconstrained), keeping
    /// its aspect ratio, as <see cref="ImageResizeMode.Max"/>; never enlarged, and itself without a box.
    /// </summary>
    public static (int Width, int Height) GetFittedSize((int Width, int Height) size, (int MaxWidth, int MaxHeight)? box)
    {
        if (box == null)
        {
            return size;
        }

        var scale = 1.0;
        if (box.Value.MaxWidth > 0 && size.Width > box.Value.MaxWidth)
        {
            scale = Math.Min(scale, (double)box.Value.MaxWidth / size.Width);
        }

        if (box.Value.MaxHeight > 0 && size.Height > box.Value.MaxHeight)
        {
            scale = Math.Min(scale, (double)box.Value.MaxHeight / size.Height);
        }

        if (scale >= 1.0)
        {
            return size;
        }

        return (Math.Max(1, (int)Math.Round(size.Width * scale)), Math.Max(1, (int)Math.Round(size.Height * scale)));
    }

    /// <summary>
    /// Whether the picture, measured as it is displayed (turned by its EXIF orientation, as the contributors turn it) and
    /// fitted into the container's box, is within WebP's limit on both sides. <c>false</c> when its header gives no size.
    /// </summary>
    private static bool FitsWebp(BlobContainerConfiguration configuration, Stream content)
    {
        var dimensions = ImageHeaderReader.Read(content);
        if (dimensions == null)
        {
            return false;
        }

        var displayed = ImageHeaderReader.SwapsSides(ImageHeaderReader.ReadExifOrientation(content))
            ? (dimensions.Value.Height, dimensions.Value.Width)
            : dimensions.Value;
        var fitted = GetFittedSize(displayed, GetBox(configuration));
        return fitted.Width <= MaxDimension && fitted.Height <= MaxDimension;
    }

    /// <summary>The bits per channel the PNG's <c>IHDR</c> declares, or <c>null</c> when it has none.</summary>
    private static int? ReadPngBitDepth(Stream content)
    {
        // Signature (8), then the IHDR chunk: length (4), type (4), width (4), height (4), bit depth (1).
        Span<byte> header = stackalloc byte[25];
        content.Position = 0;
        if (content.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length ||
            !header.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }

        return header[24];
    }
}
