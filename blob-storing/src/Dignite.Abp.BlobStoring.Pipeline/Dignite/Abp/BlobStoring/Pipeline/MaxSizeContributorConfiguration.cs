using System;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// The per-container configuration of <see cref="MaxSizeContributor"/>.
/// </summary>
public class MaxSizeContributorConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public MaxSizeContributorConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>
    /// The largest content, in bytes, the container accepts. Must be greater than zero and at most
    /// <see cref="Array.MaxLength"/>, since the content is materialized in memory. 0 when not configured.
    /// </summary>
    public long MaxSizeInBytes
    {
        get => _containerConfiguration.GetConfigurationOrDefault<long>(BlobStoringPipelineConfigurationNames.MaxSizeInBytes);
        set => _containerConfiguration.SetConfiguration(BlobStoringPipelineConfigurationNames.MaxSizeInBytes, value);
    }

    /// <summary>
    /// Throws <see cref="AbpException"/> when the configuration cannot be applied.
    /// </summary>
    public virtual void Validate()
    {
        if (MaxSizeInBytes <= 0)
        {
            throw new AbpException(
                $"{nameof(MaxSizeContributor)} requires a positive {nameof(MaxSizeInBytes)}; configure it with AddMaxSizeContributor.");
        }

        if (MaxSizeInBytes > Array.MaxLength)
        {
            throw new AbpException(
                $"{nameof(MaxSizeContributor)} buffers the content in memory, so {nameof(MaxSizeInBytes)} cannot exceed {Array.MaxLength} bytes.");
        }
    }
}
