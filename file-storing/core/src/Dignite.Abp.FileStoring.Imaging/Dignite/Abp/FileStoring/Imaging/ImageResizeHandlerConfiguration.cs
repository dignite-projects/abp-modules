using System;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.FileStoring.Imaging;

public class ImageResizeHandlerConfiguration
{
    public const int DefaultMaxImageWidth = 4096;
    public const int DefaultMaxImageHeight = 4096;
    public const long DefaultMaxPixelCount = 16_000_000;
    public const int DefaultMaxDecompressionRatio = 100;
    public const int DefaultDecodeTimeoutSeconds = 10;

    private readonly BlobContainerConfiguration _containerConfiguration;

    public ImageResizeHandlerConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = containerConfiguration;
    }

    /// <summary>
    /// Maximum width of the stored image. 0 (the default) leaves the width unconstrained.
    /// At least one of <see cref="ImageWidth"/> and <see cref="ImageHeight"/> must be set.
    /// </summary>
    public int ImageWidth
    {
        get => _containerConfiguration.GetConfigurationOrDefault<int>(ImageResizeHandlerConfigurationNames.ImageWidth);
        set => SetNonNegative(ImageResizeHandlerConfigurationNames.ImageWidth, value, nameof(ImageWidth));
    }

    /// <summary>
    /// Maximum height of the stored image. 0 (the default) leaves the height unconstrained.
    /// At least one of <see cref="ImageWidth"/> and <see cref="ImageHeight"/> must be set.
    /// </summary>
    public int ImageHeight
    {
        get => _containerConfiguration.GetConfigurationOrDefault<int>(ImageResizeHandlerConfigurationNames.ImageHeight);
        set => SetNonNegative(ImageResizeHandlerConfigurationNames.ImageHeight, value, nameof(ImageHeight));
    }

    public bool ImageSizeMustBeLargerThanPreset
    {
        get => _containerConfiguration.GetConfigurationOrDefault<bool>(ImageResizeHandlerConfigurationNames.ImageSizeMustBeLargerThanPreset, false);
        set => _containerConfiguration.SetConfiguration(ImageResizeHandlerConfigurationNames.ImageSizeMustBeLargerThanPreset, value);
    }

    public int MaxImageWidth
    {
        get => _containerConfiguration.GetConfigurationOrDefault(ImageResizeHandlerConfigurationNames.MaxImageWidth, DefaultMaxImageWidth);
        set => SetPositive(ImageResizeHandlerConfigurationNames.MaxImageWidth, value, nameof(MaxImageWidth));
    }

    public int MaxImageHeight
    {
        get => _containerConfiguration.GetConfigurationOrDefault(ImageResizeHandlerConfigurationNames.MaxImageHeight, DefaultMaxImageHeight);
        set => SetPositive(ImageResizeHandlerConfigurationNames.MaxImageHeight, value, nameof(MaxImageHeight));
    }

    public long MaxPixelCount
    {
        get => _containerConfiguration.GetConfigurationOrDefault(ImageResizeHandlerConfigurationNames.MaxPixelCount, DefaultMaxPixelCount);
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum pixel count must be greater than zero.");
            }

            _containerConfiguration.SetConfiguration(ImageResizeHandlerConfigurationNames.MaxPixelCount, value);
        }
    }

    public int MaxDecompressionRatio
    {
        get => _containerConfiguration.GetConfigurationOrDefault(ImageResizeHandlerConfigurationNames.MaxDecompressionRatio, DefaultMaxDecompressionRatio);
        set => SetPositive(ImageResizeHandlerConfigurationNames.MaxDecompressionRatio, value, nameof(MaxDecompressionRatio));
    }

    public int DecodeTimeoutSeconds
    {
        get => _containerConfiguration.GetConfigurationOrDefault(ImageResizeHandlerConfigurationNames.DecodeTimeoutSeconds, DefaultDecodeTimeoutSeconds);
        set => SetPositive(ImageResizeHandlerConfigurationNames.DecodeTimeoutSeconds, value, nameof(DecodeTimeoutSeconds));
    }

    private void SetNonNegative(string name, int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must not be negative.");
        }

        _containerConfiguration.SetConfiguration(name, value);
    }

    private void SetPositive(string name, int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must be greater than zero.");
        }

        _containerConfiguration.SetConfiguration(name, value);
    }
}
