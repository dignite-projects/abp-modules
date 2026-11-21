namespace Dignite.Abp.BlobManagement.Presets;

/// <summary>The parameters of <see cref="BlobManagementPresetExtensions.UseImagesPreset"/>.</summary>
public class BlobManagementImagesPresetOptions : BlobManagementSizePresetOptions
{
    public BlobManagementImagesPresetOptions()
        : base(BlobManagementPresetDefaults.ImagesMaxSizeInMegabytes)
    {
    }

    /// <summary>
    /// The largest width a picture is stored at, in pixels; a wider one is scaled down. 0 leaves the width
    /// unconstrained; with <see cref="MaxHeight"/> 0 as well, nothing is resized.
    /// </summary>
    public int MaxWidth { get; set; } = BlobManagementPresetDefaults.ImagesMaxWidth;

    /// <summary>The largest height a picture is stored at, in pixels; 0 leaves the height unconstrained.</summary>
    public int MaxHeight { get; set; } = BlobManagementPresetDefaults.ImagesMaxHeight;

    /// <summary>
    /// Whether a JPEG is stored as lossy WebP at <see cref="WebpQuality"/>, turned upright by its EXIF orientation and
    /// fitted into <see cref="MaxWidth"/> × <see cref="MaxHeight"/> in one encode. Its physical BLOB is named <c>.webp</c>
    /// and its type recorded as <c>image/webp</c>; an uploaded file's name takes the <c>.webp</c> extension too. A JPEG
    /// that would still be wider or taller than WebP's 16383 pixels - possible only when the box leaves a side
    /// unconstrained - is stored as a JPEG. Default: <c>true</c>. Turning it off leaves JPEGs stored before as WebP; new
    /// JPEGs are stored as JPEGs.
    /// </summary>
    public bool StoreJpegAsWebp { get; set; } = true;

    /// <summary>The quality, 1-100, of the WebP a JPEG is stored as. Default: 85.</summary>
    public int WebpQuality { get; set; } = BlobManagementPresetDefaults.ImagesWebpQuality;

    /// <summary>
    /// Whether a PNG is stored as lossless WebP - transparency kept, every visible pixel of one that fits the box kept -
    /// fitted into <see cref="MaxWidth"/> × <see cref="MaxHeight"/> in the same encode, and named and recorded as WebP as
    /// a JPEG is. A PNG is stored as it is when it is smaller than <see cref="PngWebpMinSizeInBytes"/>, has 16 bits per
    /// channel, is animated (an APNG), or would still exceed WebP's 16383 pixels. Default: <c>true</c>. Turning it off
    /// leaves PNGs stored before as WebP; new PNGs are stored as PNGs.
    /// </summary>
    public bool StorePngAsWebp { get; set; } = true;

    /// <summary>
    /// The size, in bytes, from which a PNG is stored as WebP; below it the saving is too small to be worth it. 0
    /// converts every PNG the other rules allow. Default: 64 KB.
    /// </summary>
    public long PngWebpMinSizeInBytes { get; set; } = BlobManagementPresetDefaults.ImagesPngWebpMinSizeInBytes;
}
