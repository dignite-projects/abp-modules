namespace Dignite.Drive.Presets;

/// <summary>The parameters of <see cref="DrivePresetExtensions.UseImagesPreset"/>.</summary>
public class DriveImagesPresetOptions : DriveSizePresetOptions
{
    public DriveImagesPresetOptions()
        : base(DrivePresetDefaults.ImagesMaxSizeInMegabytes)
    {
    }

    /// <summary>
    /// The largest width a picture is stored at, in pixels; a wider one is scaled down. 0 leaves the width
    /// unconstrained; with <see cref="MaxHeight"/> 0 as well, nothing is resized.
    /// </summary>
    public int MaxWidth { get; set; } = DrivePresetDefaults.ImagesMaxWidth;

    /// <summary>The largest height a picture is stored at, in pixels; 0 leaves the height unconstrained.</summary>
    public int MaxHeight { get; set; } = DrivePresetDefaults.ImagesMaxHeight;
}
