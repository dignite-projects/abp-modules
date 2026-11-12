using System;
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
        configuration.Validate();

        containerConfiguration.IsMultiTenant = true;
        return containerConfiguration;
    }
}
