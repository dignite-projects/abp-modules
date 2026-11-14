using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobManagement;

public static class BlobManagementContainerConfigurationExtensions
{
    public static BlobManagementContainerConfiguration GetBlobManagementContainerConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new BlobManagementContainerConfiguration(containerConfiguration);
    }

    /// <summary>
    /// The Blob Management configuration of <paramref name="containerName"/>; fails with
    /// <see cref="BlobManagementErrorCodes.ContainerNotFound"/> for a container the Blob Management host did not declare.
    /// </summary>
    public static BlobManagementContainerConfiguration GetBlobManagementContainerConfiguration(
        this IBlobContainerConfigurationProvider configurationProvider,
        string containerName)
    {
        Check.NotNull(configurationProvider, nameof(configurationProvider));
        Check.NotNullOrWhiteSpace(containerName, nameof(containerName));

        var configuration = configurationProvider.Get(containerName).GetBlobManagementContainerConfiguration();
        if (!configuration.IsManaged)
        {
            throw new BusinessException(BlobManagementErrorCodes.ContainerNotFound)
                .WithData("ContainerName", containerName);
        }

        return configuration;
    }

    /// <summary>Whether <paramref name="containerName"/> is a container the Blob Management host declared.</summary>
    public static bool IsManagedContainer(
        this IBlobContainerConfigurationProvider configurationProvider,
        string containerName)
    {
        Check.NotNull(configurationProvider, nameof(configurationProvider));
        return !containerName.IsNullOrWhiteSpace() &&
               configurationProvider.Get(containerName).GetBlobManagementContainerConfiguration().IsManaged;
    }

    /// <summary>
    /// The widths and heights a public read of the container's pictures may be resized to: what the container sets with
    /// <see cref="UseBlobManagementPublicSizes"/>, else what the default container sets - ABP's container configuration
    /// falls back to it - else <see cref="BlobManagementPublicUrl.DefaultSizes"/>. Ascending, without repeats.
    /// </summary>
    public static IReadOnlyList<int> GetBlobManagementPublicSizes(this BlobContainerConfiguration containerConfiguration)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        return containerConfiguration.GetConfigurationOrDefault<int[]?>(BlobManagementContainerConfigurationNames.PublicSizes, null)
               ?? BlobManagementPublicUrl.DefaultSizes;
    }

    /// <summary>
    /// Sets the widths and heights a public read may be resized to - on one container, or with
    /// <c>ConfigureDefault</c> for every container that sets none. The list replaces the one it overrides; it bounds how
    /// many renditions of a picture an anonymous visitor can make the host render. Each size is from 1 to
    /// <see cref="BlobManagementPublicUrl.MaxDimension"/>; an empty list or a size out of range throws
    /// <see cref="AbpException"/>.
    /// </summary>
    public static BlobContainerConfiguration UseBlobManagementPublicSizes(
        this BlobContainerConfiguration containerConfiguration,
        params int[] sizes)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        if (sizes == null || sizes.Length == 0)
        {
            throw new AbpException("UseBlobManagementPublicSizes needs at least one size.");
        }

        var outOfRange = sizes.Where(size => size < 1 || size > BlobManagementPublicUrl.MaxDimension).ToList();
        if (outOfRange.Count > 0)
        {
            throw new AbpException(
                $"A public read size must be from 1 to {BlobManagementPublicUrl.MaxDimension}, not {string.Join(", ", outOfRange)}.");
        }

        containerConfiguration.SetConfiguration(
            BlobManagementContainerConfigurationNames.PublicSizes,
            sizes.Distinct().OrderBy(size => size).ToArray());
        return containerConfiguration;
    }

    /// <summary>
    /// Declares the container a managed one, on the Blob Management host. Every managed container is multi-tenant, so
    /// this sets <see cref="BlobContainerConfiguration.IsMultiTenant"/> as well. Declare named containers only: a
    /// default configuration declared managed would make every container name one.
    /// </summary>
    public static BlobContainerConfiguration UseBlobManagementContainer(
        this BlobContainerConfiguration containerConfiguration,
        Action<BlobManagementContainerConfiguration>? configureAction = null)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));

        var configuration = new BlobManagementContainerConfiguration(containerConfiguration) { IsManaged = true };
        configureAction?.Invoke(configuration);

        containerConfiguration.IsMultiTenant = true;
        return containerConfiguration;
    }
}
