using System;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Imaging;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The per-container configuration of <see cref="ImageResizeContributor"/>.
/// </summary>
public class ImageResizeContributorConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public ImageResizeContributorConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>
    /// The largest width of the stored image, in pixels; 0 leaves the width unconstrained. At least one of
    /// <see cref="MaxWidth"/> and <see cref="MaxHeight"/> must be greater than zero. Default: 0.
    /// </summary>
    public int MaxWidth
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringImagingConfigurationNames.ResizeMaxWidth, 0);
        set => _containerConfiguration.SetConfiguration(BlobStoringImagingConfigurationNames.ResizeMaxWidth, value);
    }

    /// <summary>
    /// The largest height of the stored image, in pixels; 0 leaves the height unconstrained. At least one of
    /// <see cref="MaxWidth"/> and <see cref="MaxHeight"/> must be greater than zero. Default: 0.
    /// </summary>
    public int MaxHeight
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringImagingConfigurationNames.ResizeMaxHeight, 0);
        set => _containerConfiguration.SetConfiguration(BlobStoringImagingConfigurationNames.ResizeMaxHeight, value);
    }

    /// <summary>
    /// The smallest width a source image must have, in pixels; a narrower one is rejected with
    /// <see cref="BlobStoringImagingErrorCodes.ImageTooSmall"/>. 0 (the default) sets no minimum. Only enforced when the
    /// dimensions can be read from the image header.
    /// </summary>
    public int MinWidth
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringImagingConfigurationNames.ResizeMinWidth, 0);
        set => _containerConfiguration.SetConfiguration(BlobStoringImagingConfigurationNames.ResizeMinWidth, value);
    }

    /// <summary>
    /// The smallest height a source image must have, in pixels; a shorter one is rejected with
    /// <see cref="BlobStoringImagingErrorCodes.ImageTooSmall"/>. 0 (the default) sets no minimum. Only enforced when the
    /// dimensions can be read from the image header.
    /// </summary>
    public int MinHeight
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringImagingConfigurationNames.ResizeMinHeight, 0);
        set => _containerConfiguration.SetConfiguration(BlobStoringImagingConfigurationNames.ResizeMinHeight, value);
    }

    /// <summary>
    /// How the provider fits the image into <see cref="MaxWidth"/> × <see cref="MaxHeight"/>. Default:
    /// <see cref="ImageResizeMode.Max"/> (fit inside the box, keeping the aspect ratio). Whatever the mode, an image
    /// is only resized when it exceeds a constrained dimension.
    /// </summary>
    public ImageResizeMode Mode
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringImagingConfigurationNames.ResizeMode, ImageResizeMode.Max);
        set => _containerConfiguration.SetConfiguration(BlobStoringImagingConfigurationNames.ResizeMode, value);
    }

    /// <summary>
    /// Throws <see cref="AbpException"/> when the configuration cannot be applied.
    /// </summary>
    public virtual void Validate()
    {
        if (MaxWidth < 0 || MaxHeight < 0 || MinWidth < 0 || MinHeight < 0)
        {
            throw new AbpException(
                $"{nameof(ImageResizeContributor)}: {nameof(MaxWidth)}, {nameof(MaxHeight)}, {nameof(MinWidth)} and {nameof(MinHeight)} cannot be negative.");
        }

        if (MaxWidth == 0 && MaxHeight == 0)
        {
            throw new AbpException(
                $"{nameof(ImageResizeContributor)} requires a positive {nameof(MaxWidth)} or {nameof(MaxHeight)}; configure it with AddImageResizeContributor.");
        }

        if (!Enum.IsDefined(Mode))
        {
            throw new AbpException($"{nameof(ImageResizeContributor)}: '{(int)Mode}' is not a valid {nameof(ImageResizeMode)}.");
        }
    }
}
