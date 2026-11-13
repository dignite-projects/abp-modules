using System;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.BlobManagement;

public static class BlobManagementBlobContainerConfigurationExtensions
{
    public static BlobManagementBlobProviderConfiguration GetBlobManagementConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new BlobManagementBlobProviderConfiguration(containerConfiguration);
    }

    /// <summary>
    /// Keeps the container's bytes in Blob Management, in the managed container
    /// <see cref="BlobManagementBlobProviderConfiguration.BlobManagementContainer"/>. The container's own pipeline contributors run first,
    /// in the application; the managed container's run in Blob Management. Whether Blob Management is in this process or a remote
    /// service is the host's choice (<c>Dignite.Abp.BlobManagement.Application</c> or <c>Dignite.Abp.BlobManagement.HttpApi.Client</c>), not the
    /// container's.
    /// </summary>
    public static BlobContainerConfiguration UseBlobManagement(
        this BlobContainerConfiguration containerConfiguration,
        Action<BlobManagementBlobProviderConfiguration> configureAction)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));
        Check.NotNull(configureAction, nameof(configureAction));

        containerConfiguration.ProviderType = typeof(BlobManagementBlobProvider);

        var configuration = new BlobManagementBlobProviderConfiguration(containerConfiguration);
        configureAction(configuration);
        configuration.Validate();

        return containerConfiguration;
    }
}
