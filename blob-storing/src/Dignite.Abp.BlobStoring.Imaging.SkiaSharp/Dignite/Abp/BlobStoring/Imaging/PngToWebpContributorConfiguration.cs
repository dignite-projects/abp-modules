using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The per-container configuration of <see cref="PngToWebpContributor"/>.
/// </summary>
public class PngToWebpContributorConfiguration
{
    /// <summary>
    /// The size below which a PNG is stored as it is when none is given: 64 KB. Lossless WebP is about 26% smaller than
    /// PNG (Google's figure), so from 64 KB a conversion saves about 16 KB; a smaller PNG - an icon, a logo, a piece of
    /// a page - is left as the PNG it was made as, for a saving of a few KB.
    /// </summary>
    public const long DefaultMinSizeInBytes = 64 * 1024;

    private readonly BlobContainerConfiguration _containerConfiguration;

    public PngToWebpContributorConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>
    /// The size, in bytes, from which a PNG is stored as WebP; a smaller one is stored as it is, since converting it would
    /// save next to nothing. <c>0</c> converts every PNG the other rules allow. Default: <see cref="DefaultMinSizeInBytes"/>.
    /// </summary>
    public long MinSizeInBytes
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringImagingSkiaSharpConfigurationNames.PngToWebpMinSizeInBytes, DefaultMinSizeInBytes);
        set => _containerConfiguration.SetConfiguration(BlobStoringImagingSkiaSharpConfigurationNames.PngToWebpMinSizeInBytes, value);
    }

    /// <summary>
    /// Throws <see cref="AbpException"/> when the configuration cannot be applied.
    /// </summary>
    public virtual void Validate()
    {
        if (MinSizeInBytes < 0)
        {
            throw new AbpException($"{nameof(PngToWebpContributor)}: {nameof(MinSizeInBytes)} cannot be negative, not {MinSizeInBytes}.");
        }
    }
}
