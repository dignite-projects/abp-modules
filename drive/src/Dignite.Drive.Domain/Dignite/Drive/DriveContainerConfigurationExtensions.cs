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
