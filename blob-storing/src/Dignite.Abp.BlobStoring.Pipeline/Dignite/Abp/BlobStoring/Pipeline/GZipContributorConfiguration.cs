using System;
using System.IO.Compression;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// The per-container configuration of <see cref="GZipContributor"/>.
/// </summary>
public class GZipContributorConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public GZipContributorConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>
    /// The compression level of newly saved BLOBs. Default: <see cref="System.IO.Compression.CompressionLevel.Optimal"/>.
    /// Changing it does not affect reading existing BLOBs.
    /// </summary>
    public CompressionLevel CompressionLevel
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobStoringPipelineConfigurationNames.GZipCompressionLevel, CompressionLevel.Optimal);
        set => _containerConfiguration.SetConfiguration(BlobStoringPipelineConfigurationNames.GZipCompressionLevel, value);
    }

    /// <summary>
    /// Throws <see cref="AbpException"/> when the configuration cannot be applied.
    /// </summary>
    public virtual void Validate()
    {
        if (!Enum.IsDefined(CompressionLevel))
        {
            throw new AbpException($"{nameof(GZipContributor)}: '{(int)CompressionLevel}' is not a valid {nameof(CompressionLevel)}.");
        }
    }
}
