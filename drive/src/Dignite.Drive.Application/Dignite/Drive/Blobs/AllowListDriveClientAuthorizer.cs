using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;

namespace Dignite.Drive.Blobs;

/// <summary>
/// Allows a client in a system container whose <see cref="DriveContainerConfiguration.AllowedClientIds"/> lists it,
/// compared exactly. A user drive, a container the Drive did not declare and an empty list allow no client.
/// </summary>
public class AllowListDriveClientAuthorizer : IDriveClientAuthorizer, ITransientDependency
{
    protected IBlobContainerConfigurationProvider ConfigurationProvider { get; }

    public AllowListDriveClientAuthorizer(IBlobContainerConfigurationProvider configurationProvider)
    {
        ConfigurationProvider = configurationProvider;
    }

    public virtual Task<bool> IsAllowedAsync(string containerName, string clientId)
    {
        var drive = ConfigurationProvider.Get(containerName).GetDriveContainerConfiguration();

        return Task.FromResult(
            drive.IsDriveContainer &&
            drive.Kind == DriveContainerKind.System &&
            drive.AllowedClientIds.Contains(clientId, StringComparer.Ordinal));
    }
}
