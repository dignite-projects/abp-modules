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
    /// and its type recorded as <c>image/webp</c>; in a user drive its name takes the <c>.webp</c> extension too. Default:
    /// <c>true</c>. Turning it off leaves JPEGs stored before as WebP; new JPEGs are stored as JPEGs.
    /// </summary>
    public bool StoreJpegAsWebp { get; set; } = true;

    /// <summary>The quality, 1-100, of the WebP a JPEG is stored as. Default: 85.</summary>
    public int WebpQuality { get; set; } = BlobManagementPresetDefaults.ImagesWebpQuality;
}
