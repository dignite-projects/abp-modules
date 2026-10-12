namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The <see cref="Volo.Abp.BlobStoring.BlobContainerConfiguration"/> entries the image contributors read.
/// Prefer the typed wrappers (<see cref="ImageResizeContributorConfiguration"/>,
/// <see cref="ImageDecodeGuardConfiguration"/>) over setting these directly.
/// </summary>
public static class BlobStoringImagingConfigurationNames
{
    private const string Prefix = "Dignite.Abp.BlobStoring.Imaging.";

    /// <summary>
    /// <see cref="ImageResizeContributor"/>: the maximum width of the stored image (<see cref="int"/>).
    /// </summary>
    public const string ResizeMaxWidth = Prefix + "Resize.MaxWidth";

    /// <summary>
    /// <see cref="ImageResizeContributor"/>: the maximum height of the stored image (<see cref="int"/>).
    /// </summary>
    public const string ResizeMaxHeight = Prefix + "Resize.MaxHeight";

    /// <summary>
    /// <see cref="ImageResizeContributor"/>: the minimum width of the source image (<see cref="int"/>).
    /// </summary>
    public const string ResizeMinWidth = Prefix + "Resize.MinWidth";

    /// <summary>
    /// <see cref="ImageResizeContributor"/>: the minimum height of the source image (<see cref="int"/>).
    /// </summary>
    public const string ResizeMinHeight = Prefix + "Resize.MinHeight";

    /// <summary>
    /// <see cref="ImageResizeContributor"/>: the <see cref="Volo.Abp.Imaging.ImageResizeMode"/>.
    /// </summary>
    public const string ResizeMode = Prefix + "Resize.Mode";

    /// <summary>
    /// Decode guard: the maximum width of a source image (<see cref="int"/>).
    /// </summary>
    public const string DecodeGuardMaxSourceWidth = Prefix + "DecodeGuard.MaxSourceWidth";

    /// <summary>
    /// Decode guard: the maximum height of a source image (<see cref="int"/>).
    /// </summary>
    public const string DecodeGuardMaxSourceHeight = Prefix + "DecodeGuard.MaxSourceHeight";

    /// <summary>
    /// Decode guard: the maximum pixel count of a source image (<see cref="long"/>).
    /// </summary>
    public const string DecodeGuardMaxSourcePixels = Prefix + "DecodeGuard.MaxSourcePixels";

    /// <summary>
    /// Decode guard: the maximum pixels per stored byte (<see cref="int"/>).
    /// </summary>
    public const string DecodeGuardMaxDecompressionRatio = Prefix + "DecodeGuard.MaxDecompressionRatio";

    /// <summary>
    /// Decode guard: the processing timeout (<see cref="System.TimeSpan"/>).
    /// </summary>
    public const string DecodeGuardDecodeTimeout = Prefix + "DecodeGuard.DecodeTimeout";
}
