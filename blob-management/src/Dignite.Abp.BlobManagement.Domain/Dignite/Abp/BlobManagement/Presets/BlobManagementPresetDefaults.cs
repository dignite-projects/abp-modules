namespace Dignite.Abp.BlobManagement.Presets;

/// <summary>
/// The presets' default parameters - the limits Site's own containers used before they moved to Blob Management.
/// </summary>
public static class BlobManagementPresetDefaults
{
    public const long ImagesMaxSizeInMegabytes = 25;

    public const int ImagesMaxWidth = 2560;

    public const int ImagesMaxHeight = 4096;

    public const int ImagesWebpQuality = 85;

    public const long DocumentsMaxSizeInMegabytes = 20;

    public const long BinaryAssetsMaxSizeInMegabytes = 5;

    public const long AnyMaxSizeInMegabytes = 20;
}
