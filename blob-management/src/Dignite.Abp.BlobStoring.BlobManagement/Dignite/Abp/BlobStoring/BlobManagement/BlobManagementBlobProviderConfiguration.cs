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

    /// <summary>
    /// Whether anyone may read the container's BLOBs at their public address (<see cref="IBlobManagementPublicUrlBuilder"/>),
    /// without signing in. The application decides it, and the provider sends it with every save, so it applies to
    /// what is saved from then on; a BLOB saved before keeps what it was saved with until it is saved again. Default:
    /// <c>false</c>.
    /// </summary>
    public bool IsPublic
    {
        get => _containerConfiguration.GetConfigurationOrDefault(BlobManagementBlobProviderConfigurationNames.IsPublic, false);
        set => _containerConfiguration.SetConfiguration(BlobManagementBlobProviderConfigurationNames.IsPublic, value);
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
