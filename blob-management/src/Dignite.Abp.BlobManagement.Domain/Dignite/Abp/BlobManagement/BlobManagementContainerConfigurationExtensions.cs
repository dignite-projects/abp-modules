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
    /// <see cref="BlobManagementErrorCodes.ContainerNotFound"/> for a container the Blob Management host did not declare, or one of
    /// another <paramref name="kind"/> than asked for - to the entry point asking, a container of the other kind does
    /// not exist.
    /// </summary>
    public static BlobManagementContainerConfiguration GetBlobManagementContainerConfiguration(
        this IBlobContainerConfigurationProvider configurationProvider,
        string containerName,
        BlobManagementContainerKind? kind = null)
    {
        Check.NotNull(configurationProvider, nameof(configurationProvider));
        Check.NotNullOrWhiteSpace(containerName, nameof(containerName));

        var configuration = configurationProvider.Get(containerName).GetBlobManagementContainerConfiguration();
        if (!configuration.IsBlobManagementContainer || (kind.HasValue && configuration.Kind != kind.Value))
        {
            throw new BusinessException(BlobManagementErrorCodes.ContainerNotFound)
                .WithData("ContainerName", containerName);
        }

        return configuration;
    }

    /// <summary>
    /// Declares the container one of the BlobManagement's, on the Blob Management host. Every Blob Management container is multi-tenant, so this
    /// sets <see cref="BlobContainerConfiguration.IsMultiTenant"/> as well. Declare named containers only: a default
    /// configuration declared a Blob Management container would make every container name one.
    /// </summary>
    public static BlobContainerConfiguration UseBlobManagementContainer(
        this BlobContainerConfiguration containerConfiguration,
        Action<BlobManagementContainerConfiguration>? configureAction = null)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));

        var configuration = new BlobManagementContainerConfiguration(containerConfiguration);
        if (!configuration.IsBlobManagementContainer)
        {
            configuration.Kind = BlobManagementContainerKind.System;
        }

        configureAction?.Invoke(configuration);
        configuration.Validate();

        containerConfiguration.IsMultiTenant = true;
        return containerConfiguration;
    }
}
