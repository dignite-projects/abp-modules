using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The per-container configuration of <see cref="ImageCompressContributor"/>. It has no options of its own yet (the
/// encoder quality is the imaging provider's, for example <c>SkiaSharpCompressOptions</c>); the decode limits are in
/// <see cref="ImageDecodeGuardConfiguration"/>. It exists so options can be added without breaking the API.
/// </summary>
public class ImageCompressContributorConfiguration
{
    public ImageCompressContributorConfiguration(BlobContainerConfiguration containerConfiguration)
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
