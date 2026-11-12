using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// Allows a client in a system container whose <see cref="BlobManagementContainerConfiguration.AllowedClientIds"/> lists it,
/// compared exactly. A user drive, a container Blob Management did not declare and an empty list allow no client.
/// </summary>
public class AllowListBlobManagementClientAuthorizer : IBlobManagementClientAuthorizer, ITransientDependency
{
    protected IBlobContainerConfigurationProvider ConfigurationProvider { get; }

    public AllowListBlobManagementClientAuthorizer(IBlobContainerConfigurationProvider configurationProvider)
    {
        ConfigurationProvider = configurationProvider;
    }

    public virtual Task<bool> IsAllowedAsync(string containerName, string clientId)
    {
        var drive = ConfigurationProvider.Get(containerName).GetBlobManagementContainerConfiguration();

        return Task.FromResult(
            drive.IsBlobManagementContainer &&
            drive.Kind == BlobManagementContainerKind.System &&
            drive.AllowedClientIds.Contains(clientId, StringComparer.Ordinal));
    }
}
