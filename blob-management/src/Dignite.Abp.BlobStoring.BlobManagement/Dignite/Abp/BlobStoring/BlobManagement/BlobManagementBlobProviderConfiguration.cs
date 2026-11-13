using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.BlobManagement;

/// <summary>
/// The Blob Management provider's configuration of an application's container, kept in its <see cref="BlobContainerConfiguration"/>.
/// </summary>
public class BlobManagementBlobProviderConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public BlobManagementBlobProviderConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>
    /// The managed container the bytes go to, declared on the Blob Management host. It need not have the application
    /// container's name. Application containers that share one share its names too: a name saved through one is the
    /// same file through the other.
    /// </summary>
    public string BlobManagementContainer
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobManagementBlobProviderConfigurationNames.BlobManagementContainer, string.Empty)!;
        set => _containerConfiguration.SetConfiguration(BlobManagementBlobProviderConfigurationNames.BlobManagementContainer, value);
    }

    /// <summary>Throws <see cref="AbpException"/> when the configuration cannot be applied.</summary>
    public virtual void Validate()
    {
        if (string.IsNullOrWhiteSpace(BlobManagementContainer))
        {
            throw new AbpException($"UseBlobManagement requires a {nameof(BlobManagementContainer)}: the managed container the bytes go to.");
        }
    }
}
