using System;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// Adds this package's contributors to a container. Each <c>Add…Contributor</c> applies the configuration,
/// validates it (throwing <see cref="AbpException"/> on an invalid setup) and appends the contributor to
/// <see cref="BlobContainerConfiguration.PipelineContributors"/> once; calling it again only updates the
/// configuration. Contributors run in the order they were added while saving (in reverse while reading).
/// </summary>
public static class BlobContainerConfigurationExtensions
{
    public static MaxSizeContributorConfiguration GetMaxSizeContributorConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new MaxSizeContributorConfiguration(containerConfiguration);
    }

    /// <summary>
    /// Adds <see cref="MaxSizeContributor"/>; set <see cref="MaxSizeContributorConfiguration.MaxSizeInBytes"/>.
    /// Add it first, so later contributors work on its bounded, seekable copy.
    /// </summary>
    public static BlobContainerConfiguration AddMaxSizeContributor(
        this BlobContainerConfiguration containerConfiguration,
        Action<MaxSizeContributorConfiguration> configureAction)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        Check.NotNull(configureAction, nameof(configureAction));

        var configuration = new MaxSizeContributorConfiguration(containerConfiguration);
        configureAction(configuration);
        configuration.Validate();

        containerConfiguration.PipelineContributors.TryAdd<MaxSizeContributor>();
        return containerConfiguration;
    }

    public static AllowedContentTypesContributorConfiguration GetAllowedContentTypesContributorConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new AllowedContentTypesContributorConfiguration(containerConfiguration);
    }

    /// <summary>
    /// Adds <see cref="AllowedContentTypesContributor"/>; set
    /// <see cref="AllowedContentTypesContributorConfiguration.AllowedContentTypes"/>. Add it before any
    /// transforming contributor.
    /// </summary>
    public static BlobContainerConfiguration AddAllowedContentTypesContributor(
        this BlobContainerConfiguration containerConfiguration,
        Action<AllowedContentTypesContributorConfiguration> configureAction)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        Check.NotNull(configureAction, nameof(configureAction));

        var configuration = new AllowedContentTypesContributorConfiguration(containerConfiguration);
        configureAction(configuration);
        configuration.Validate();

        containerConfiguration.PipelineContributors.TryAdd<AllowedContentTypesContributor>();
        return containerConfiguration;
    }

    public static GZipContributorConfiguration GetGZipContributorConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new GZipContributorConfiguration(containerConfiguration);
    }

    /// <summary>
    /// Adds <see cref="GZipContributor"/>, optionally setting
    /// <see cref="GZipContributorConfiguration.CompressionLevel"/>. It changes the stored format: do not add it
    /// to (or remove it from) a container that already has BLOBs.
    /// </summary>
    public static BlobContainerConfiguration AddGZipContributor(
        this BlobContainerConfiguration containerConfiguration,
        Action<GZipContributorConfiguration>? configureAction = null)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));

        var configuration = new GZipContributorConfiguration(containerConfiguration);
        configureAction?.Invoke(configuration);
        configuration.Validate();

        containerConfiguration.PipelineContributors.TryAdd<GZipContributor>();
        return containerConfiguration;
    }
}
