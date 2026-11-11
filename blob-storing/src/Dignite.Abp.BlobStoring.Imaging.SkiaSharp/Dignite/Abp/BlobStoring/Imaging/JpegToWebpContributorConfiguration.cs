using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The per-container configuration of <see cref="JpegToWebpContributor"/>.
/// </summary>
public class JpegToWebpContributorConfiguration
{
    /// <summary>The quality a JPEG is stored at when none is given: 85.</summary>
    public const int DefaultQuality = 85;

    private readonly BlobContainerConfiguration _containerConfiguration;

    public JpegToWebpContributorConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>
    /// The quality, 1-100, of the lossy WebP a JPEG is stored as. Default: <see cref="DefaultQuality"/>.
    /// </summary>
    public int Quality
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringImagingSkiaSharpConfigurationNames.JpegToWebpQuality, DefaultQuality);
        set => _containerConfiguration.SetConfiguration(BlobStoringImagingSkiaSharpConfigurationNames.JpegToWebpQuality, value);
    }

    /// <summary>
    /// Throws <see cref="AbpException"/> when the configuration cannot be applied.
    /// </summary>
    public virtual void Validate()
    {
        if (Quality is < 1 or > 100)
        {
            throw new AbpException($"{nameof(JpegToWebpContributor)}: {nameof(Quality)} must be between 1 and 100, not {Quality}.");
        }
    }
}
