using System;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Adds this package's contributors to a container. Each <c>Add…Contributor</c> applies the configuration, validates
/// it (throwing <see cref="AbpException"/> on an invalid setup) and appends the contributor to
/// <see cref="BlobContainerConfiguration.PipelineContributors"/> once; calling it again only updates the configuration.
/// Contributors run in the order they were added while saving.
/// <para>
/// Named apart from <c>Dignite.Abp.BlobStoring.Pipeline.BlobContainerConfigurationExtensions</c> so that a file
/// importing both namespaces can still name either class.
/// </para>
/// </summary>
public static class ImagingBlobContainerConfigurationExtensions
{
    public static ImageResizeContributorConfiguration GetImageResizeContributorConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new ImageResizeContributorConfiguration(containerConfiguration);
    }

    /// <summary>
    /// Adds <see cref="ImageResizeContributor"/>; set <see cref="ImageResizeContributorConfiguration.MaxWidth"/> and/or
    /// <see cref="ImageResizeContributorConfiguration.MaxHeight"/>. Add it after <c>AddMaxSizeContributor</c> and
    /// <c>AddAllowedContentTypesContributor</c>, and before <see cref="AddImageCompressContributor"/>.
    /// </summary>
    public static BlobContainerConfiguration AddImageResizeContributor(
        this BlobContainerConfiguration containerConfiguration,
        Action<ImageResizeContributorConfiguration> configureAction)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        Check.NotNull(configureAction, nameof(configureAction));

        var configuration = new ImageResizeContributorConfiguration(containerConfiguration);
        configureAction(configuration);
        configuration.Validate();

        containerConfiguration.PipelineContributors.TryAdd<ImageResizeContributor>();
        return containerConfiguration;
    }

    public static ImageCompressContributorConfiguration GetImageCompressContributorConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new ImageCompressContributorConfiguration(containerConfiguration);
    }

    /// <summary>
    /// Adds <see cref="ImageCompressContributor"/>. Add it after <see cref="AddImageResizeContributor"/>, so it
    /// compresses the resized image.
    /// </summary>
    public static BlobContainerConfiguration AddImageCompressContributor(
        this BlobContainerConfiguration containerConfiguration,
        Action<ImageCompressContributorConfiguration>? configureAction = null)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));

        var configuration = new ImageCompressContributorConfiguration(containerConfiguration);
        configureAction?.Invoke(configuration);
        configuration.Validate();

        containerConfiguration.PipelineContributors.TryAdd<ImageCompressContributor>();
        return containerConfiguration;
    }

    public static ImageDecodeGuardConfiguration GetImageDecodeGuardConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new ImageDecodeGuardConfiguration(containerConfiguration);
    }

    /// <summary>
    /// Changes the decode guard of the container, which both image contributors apply before handing an image to the
    /// imaging provider. Optional: the <see cref="ImageDecodeGuardConfiguration"/> defaults apply when it is never
    /// called. It does not add a contributor.
    /// </summary>
    public static BlobContainerConfiguration ConfigureImageDecodeGuard(
        this BlobContainerConfiguration containerConfiguration,
        Action<ImageDecodeGuardConfiguration> configureAction)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        Check.NotNull(configureAction, nameof(configureAction));

        var configuration = new ImageDecodeGuardConfiguration(containerConfiguration);
        configureAction(configuration);
        configuration.Validate();

        return containerConfiguration;
    }
}
