using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The per-container configuration of <see cref="ImageMetadataStripContributor"/>. It has no options of its own yet:
/// what is removed is fixed (see the contributor). It exists so options can be added without breaking the API.
/// </summary>
public class ImageMetadataStripContributorConfiguration
{
    public ImageMetadataStripContributorConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        ContainerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>
    /// The container configuration the options are stored in.
    /// </summary>
    protected BlobContainerConfiguration ContainerConfiguration { get; }

    /// <summary>
    /// Throws <see cref="AbpException"/> when the configuration cannot be applied.
    /// </summary>
    public virtual void Validate()
    {
    }
}
