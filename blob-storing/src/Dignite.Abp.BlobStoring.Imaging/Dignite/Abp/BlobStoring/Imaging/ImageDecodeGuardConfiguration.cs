using System;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The per-container decode guard shared by <see cref="ImageResizeContributor"/> and
/// <see cref="ImageCompressContributor"/>: the limits an image must stay within before it is handed to the imaging
/// provider, which decodes it into memory. Set it with
/// <see cref="ImagingBlobContainerConfigurationExtensions.ConfigureImageDecodeGuard"/>; the defaults apply when it is
/// never configured.
/// <para>
/// The dimensions are read from the image header (PNG, JPEG, GIF, BMP, WebP, TIFF) without decoding. When the header
/// cannot be read (another format, or a header this reader does not understand), the dimension, pixel and ratio
/// limits are skipped; the <see cref="DecodeTimeout"/> and the container's buffering cap still apply.
/// </para>
/// </summary>
public class ImageDecodeGuardConfiguration
{
    public const int DefaultMaxSourceWidth = 8192;

    public const int DefaultMaxSourceHeight = 8192;

    public const long DefaultMaxSourcePixels = 50_000_000;

    public const int DefaultMaxDecompressionRatio = 100;

    public static readonly TimeSpan DefaultDecodeTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The longest <see cref="DecodeTimeout"/> a <see cref="System.Threading.CancellationTokenSource"/> accepts.
    /// </summary>
    public static readonly TimeSpan MaxDecodeTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

    private readonly BlobContainerConfiguration _containerConfiguration;

    public ImageDecodeGuardConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>
    /// The widest source image accepted, in pixels. Default: <see cref="DefaultMaxSourceWidth"/> (8192).
    /// </summary>
    public int MaxSourceWidth
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringImagingConfigurationNames.DecodeGuardMaxSourceWidth, DefaultMaxSourceWidth);
        set => _containerConfiguration.SetConfiguration(BlobStoringImagingConfigurationNames.DecodeGuardMaxSourceWidth, value);
    }

    /// <summary>
    /// The tallest source image accepted, in pixels. Default: <see cref="DefaultMaxSourceHeight"/> (8192).
    /// </summary>
    public int MaxSourceHeight
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringImagingConfigurationNames.DecodeGuardMaxSourceHeight, DefaultMaxSourceHeight);
        set => _containerConfiguration.SetConfiguration(BlobStoringImagingConfigurationNames.DecodeGuardMaxSourceHeight, value);
    }

    /// <summary>
    /// The largest source image accepted, in pixels (width × height). Default:
    /// <see cref="DefaultMaxSourcePixels"/> (50,000,000, which admits a 24 MP camera original). A decoded image is
    /// held as roughly four bytes per pixel, so the default is about 200 MB per decode in the worst case; lower it for
    /// containers that take uploads from many users at once.
    /// </summary>
    public long MaxSourcePixels
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringImagingConfigurationNames.DecodeGuardMaxSourcePixels, DefaultMaxSourcePixels);
        set => _containerConfiguration.SetConfiguration(BlobStoringImagingConfigurationNames.DecodeGuardMaxSourcePixels, value);
    }

    /// <summary>
    /// The most pixels a source image may declare per stored byte. A small file that declares a huge canvas (a
    /// decompression bomb) is rejected before it is decoded. Default: <see cref="DefaultMaxDecompressionRatio"/> (100).
    /// Images with large uniform areas compress well, so lower it with care.
    /// </summary>
    public int MaxDecompressionRatio
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringImagingConfigurationNames.DecodeGuardMaxDecompressionRatio, DefaultMaxDecompressionRatio);
        set => _containerConfiguration.SetConfiguration(BlobStoringImagingConfigurationNames.DecodeGuardMaxDecompressionRatio, value);
    }

    /// <summary>
    /// How long the imaging provider may take to process one image; beyond it the save fails with
    /// <see cref="BlobStoringImagingErrorCodes.ImageDecodeTimeout"/>. Default: <see cref="DefaultDecodeTimeout"/>
    /// (10 seconds). It works through the cancellation token passed to the provider, so it only interrupts a provider
    /// that observes that token while decoding (ImageSharp does; SkiaSharp and Magick.NET decode synchronously), so for
    /// those the dimension and pixel limits and the buffering cap are the protection that applies.
    /// </summary>
    public TimeSpan DecodeTimeout
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringImagingConfigurationNames.DecodeGuardDecodeTimeout, DefaultDecodeTimeout);
        set => _containerConfiguration.SetConfiguration(BlobStoringImagingConfigurationNames.DecodeGuardDecodeTimeout, value);
    }

    /// <summary>
    /// Throws <see cref="AbpException"/> when the configuration cannot be applied.
    /// </summary>
    public virtual void Validate()
    {
        RequirePositive(MaxSourceWidth, nameof(MaxSourceWidth));
        RequirePositive(MaxSourceHeight, nameof(MaxSourceHeight));
        RequirePositive(MaxSourcePixels, nameof(MaxSourcePixels));
        RequirePositive(MaxDecompressionRatio, nameof(MaxDecompressionRatio));

        if (DecodeTimeout <= TimeSpan.Zero || DecodeTimeout > MaxDecodeTimeout)
        {
            throw new AbpException(
                $"Image decode guard: {nameof(DecodeTimeout)} must be greater than zero and at most {MaxDecodeTimeout}.");
        }
    }

    private static void RequirePositive(long value, string name)
    {
        if (value <= 0)
        {
            throw new AbpException($"Image decode guard: {name} must be greater than zero.");
        }
    }
}
