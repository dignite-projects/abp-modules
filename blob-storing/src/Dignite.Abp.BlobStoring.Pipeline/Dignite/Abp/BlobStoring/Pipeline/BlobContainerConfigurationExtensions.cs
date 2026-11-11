using System;
using System.Collections.Generic;
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

    /// <summary>
    /// The MIME type that content detected as <paramref name="contentType"/> is stored as in this container - what a read
    /// of the blob through the container returns: <paramref name="contentType"/> itself, unless a contributor that
    /// converts it declared otherwise with <see cref="SetStoredContentType"/> (for example <c>image/jpeg</c> stored as
    /// <c>image/webp</c>). Compared case-insensitively.
    /// <para>
    /// A static declaration, read from the configuration before anything is saved, for a caller that names its blobs or
    /// records their type before the save: it is not a result of the save. A caller that has to know what was actually
    /// stored detects it from the stored bytes after the save.
    /// </para>
    /// </summary>
    public static string GetStoredContentType(this BlobContainerConfiguration containerConfiguration, string contentType)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        Check.NotNullOrWhiteSpace(contentType, nameof(contentType));

        var storedContentTypes = containerConfiguration
            .GetConfigurationOrDefault<IReadOnlyDictionary<string, string>?>(BlobStoringPipelineConfigurationNames.StoredContentTypes);
        return storedContentTypes != null && storedContentTypes.TryGetValue(contentType, out var storedContentType)
            ? storedContentType
            : contentType;
    }

    /// <summary>
    /// Declares that the container stores content detected as <paramref name="contentType"/> as
    /// <paramref name="storedContentType"/>: called by whoever adds a contributor that converts every such content, so
    /// <see cref="GetStoredContentType"/> tells a caller before the save. Only a contributor that always converts may
    /// declare it - one that converts only when the result is smaller cannot. One step: a type that is itself converted
    /// is not followed. Declaring a type again replaces its earlier declaration; declarations on the default container
    /// apply to the others until a container declares its own.
    /// </summary>
    public static BlobContainerConfiguration SetStoredContentType(
        this BlobContainerConfiguration containerConfiguration,
        string contentType,
        string storedContentType)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        Check.NotNullOrWhiteSpace(contentType, nameof(contentType));
        Check.NotNullOrWhiteSpace(storedContentType, nameof(storedContentType));

        // A new dictionary each time: the one read may be the default container's, which other containers share.
        var storedContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var current = containerConfiguration
            .GetConfigurationOrDefault<IReadOnlyDictionary<string, string>?>(BlobStoringPipelineConfigurationNames.StoredContentTypes);
        if (current != null)
        {
            foreach (var (from, to) in current)
            {
                storedContentTypes[from] = to;
            }
        }

        storedContentTypes[contentType] = storedContentType;
        containerConfiguration.SetConfiguration(
            BlobStoringPipelineConfigurationNames.StoredContentTypes,
            (IReadOnlyDictionary<string, string>)storedContentTypes);
        return containerConfiguration;
    }
}
