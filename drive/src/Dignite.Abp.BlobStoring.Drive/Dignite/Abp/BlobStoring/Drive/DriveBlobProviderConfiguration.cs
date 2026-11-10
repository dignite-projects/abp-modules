using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Drive;

/// <summary>
/// The Drive provider's configuration of an application's container, kept in its <see cref="BlobContainerConfiguration"/>.
/// </summary>
public class DriveBlobProviderConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public DriveBlobProviderConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>
    /// The Drive system container the bytes go to, declared on the Drive host. It need not have the application
    /// container's name. Application containers that share one share its names too: a name saved through one is the
    /// same file through the other.
    /// </summary>
    public string DriveContainer
    {
        get => _containerConfiguration.GetConfigurationOrDefault(DriveBlobProviderConfigurationNames.DriveContainer, string.Empty)!;
        set => _containerConfiguration.SetConfiguration(DriveBlobProviderConfigurationNames.DriveContainer, value);
    }

    /// <summary>Throws <see cref="AbpException"/> when the configuration cannot be applied.</summary>
    public virtual void Validate()
    {
        if (string.IsNullOrWhiteSpace(DriveContainer))
        {
            throw new AbpException($"UseDrive requires a {nameof(DriveContainer)}: the Drive container the bytes go to.");
        }
    }
}
