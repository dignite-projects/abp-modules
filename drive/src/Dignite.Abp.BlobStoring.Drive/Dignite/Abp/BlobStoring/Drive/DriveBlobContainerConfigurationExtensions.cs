using System;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Drive;

public static class DriveBlobContainerConfigurationExtensions
{
    public static DriveBlobProviderConfiguration GetDriveConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new DriveBlobProviderConfiguration(containerConfiguration);
    }

    /// <summary>
    /// Keeps the container's bytes in the Drive, in the system container
    /// <see cref="DriveBlobProviderConfiguration.DriveContainer"/>. The container's own pipeline contributors run first,
    /// in the application; the Drive container's run on the Drive. Whether the Drive is in this process or a remote
    /// service is the host's choice (<c>Dignite.Drive.Application</c> or <c>Dignite.Drive.HttpApi.Client</c>), not the
    /// container's.
    /// </summary>
    public static BlobContainerConfiguration UseDrive(
        this BlobContainerConfiguration containerConfiguration,
        Action<DriveBlobProviderConfiguration> configureAction)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        Check.NotNull(configureAction, nameof(configureAction));

        containerConfiguration.ProviderType = typeof(DriveBlobProvider);

        var configuration = new DriveBlobProviderConfiguration(containerConfiguration);
        configureAction(configuration);
        configuration.Validate();

        return containerConfiguration;
    }
}
