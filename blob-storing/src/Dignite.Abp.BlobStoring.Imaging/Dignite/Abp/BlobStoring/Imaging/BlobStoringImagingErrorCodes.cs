namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The <see cref="Volo.Abp.BusinessException"/> codes thrown by the image contributors; localized by
/// <c>BlobStoringImagingResource</c>. The namespace differs from the Pipeline package's
/// (<c>Dignite.Abp.BlobStoring</c>) because ABP maps each code namespace to a single localization resource.
/// </summary>
public static class BlobStoringImagingErrorCodes
{
    public const string Namespace = "Dignite.Abp.BlobStoring.Imaging";

    /// <summary>
    /// The image exceeds the decode guard (<see cref="ImageDecodeGuardConfiguration"/>): its dimensions, its pixel
    /// count, or its pixels per stored byte. Data: <c>Width</c>, <c>Height</c>, <c>MaxWidth</c>, <c>MaxHeight</c>,
    /// <c>MaxPixels</c>, <c>MaxDecompressionRatio</c>.
    /// </summary>
    public const string ImageTooLarge = Namespace + ":ImageTooLarge";

    /// <summary>
    /// The image is smaller than <see cref="ImageResizeContributorConfiguration.MinWidth"/> /
    /// <see cref="ImageResizeContributorConfiguration.MinHeight"/>. Data: <c>Width</c>, <c>Height</c>,
    /// <c>MinWidth</c>, <c>MinHeight</c>.
    /// </summary>
    public const string ImageTooSmall = Namespace + ":ImageTooSmall";

    /// <summary>
    /// Processing the image took longer than <see cref="ImageDecodeGuardConfiguration.DecodeTimeout"/>.
    /// Data: <c>DecodeTimeoutSeconds</c>.
    /// </summary>
    public const string ImageDecodeTimeout = Namespace + ":ImageDecodeTimeout";

    /// <summary>
    /// The imaging provider could not process the image: it threw (for example on corrupt image data), or a resize
    /// ended <see cref="Volo.Abp.Imaging.ImageProcessState.Canceled"/>.
    /// </summary>
    public const string ImageProcessingFailed = Namespace + ":ImageProcessingFailed";
}
