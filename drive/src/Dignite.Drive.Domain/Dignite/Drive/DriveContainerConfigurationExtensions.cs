using System;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Drive;

public static class DriveContainerConfigurationExtensions
{
    public static DriveContainerConfiguration GetDriveContainerConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new DriveContainerConfiguration(containerConfiguration);
    }

    /// <summary>
    /// The Drive configuration of <paramref name="containerName"/>; fails with
    /// <see cref="DriveErrorCodes.ContainerNotFound"/> for a container the Drive host did not declare, or one of
    /// another <paramref name="kind"/> than asked for - to the entry point asking, a container of the other kind does
    /// not exist.
    /// </summary>
    public static DriveContainerConfiguration GetDriveContainerConfiguration(
        this IBlobContainerConfigurationProvider configurationProvider,
        string containerName,
        DriveContainerKind? kind = null)
    {
        Check.NotNull(configurationProvider, nameof(configurationProvider));
        Check.NotNullOrWhiteSpace(containerName, nameof(containerName));

        var configuration = configurationProvider.Get(containerName).GetDriveContainerConfiguration();
        if (!configuration.IsDriveContainer || (kind.HasValue && configuration.Kind != kind.Value))
        {
            throw new BusinessException(DriveErrorCodes.ContainerNotFound)
                .WithData("ContainerName", containerName);
        }

        return configuration;
    }

    /// <summary>
    /// Declares the container one of the Drive's, on the Drive host. Every Drive container is multi-tenant, so this
    /// sets <see cref="BlobContainerConfiguration.IsMultiTenant"/> as well. Declare named containers only: a default
    /// configuration declared a Drive container would make every container name one.
    /// </summary>
    public static BlobContainerConfiguration UseDriveContainer(
        this BlobContainerConfiguration containerConfiguration,
        Action<DriveContainerConfiguration>? configureAction = null)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));

        var configuration = new DriveContainerConfiguration(containerConfiguration);
        if (!configuration.IsDriveContainer)
        {
            configuration.Kind = DriveContainerKind.System;
        }

        configureAction?.Invoke(configuration);
        configuration.Validate();

        containerConfiguration.IsMultiTenant = true;
        return containerConfiguration;
    }
}
