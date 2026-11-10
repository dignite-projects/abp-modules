using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Clients;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;

namespace Dignite.Drive.Blobs;

/// <summary>
/// The BLOB API's <see cref="IDriveBlobService"/>: the in-process implementation behind an authorization of the caller.
/// <list type="bullet">
/// <item>The caller must be an OAuth client (a <c>client_id</c> claim).</item>
/// <item>A token that carries a user is refused, whatever its client: a user token carries <c>client_id</c> too, so an
/// allowed first-party client would otherwise let its users write. Browsers reach the Drive through tickets.</item>
/// <item>The client must be allowed in the container (<see cref="IDriveClientAuthorizer"/>).</item>
/// </list>
/// The writer is recorded as the client. Exposed only through <c>DriveBlobController</c>.
/// </summary>
[Authorize]
[RemoteService(IsEnabled = false)]
[ExposeServices(typeof(IDriveBlobAppService), typeof(DriveBlobAppService))]
public class DriveBlobAppService : DriveBlobService, IDriveBlobAppService
{
    protected IDriveClientAuthorizer ClientAuthorizer { get; }

    protected ICurrentClient CurrentClient { get; }

    public DriveBlobAppService(
        FileDescriptorManager fileDescriptorManager,
        IDriveClientAuthorizer clientAuthorizer,
        ICurrentClient currentClient)
        : base(fileDescriptorManager)
    {
        ClientAuthorizer = clientAuthorizer;
        CurrentClient = currentClient;
    }

    public override async Task<DriveBlobDto> SaveAsync(string containerName, string blobName, IRemoteStreamContent content, bool overrideExisting = false)
    {
        var clientId = await CheckClientAsync(containerName);
        return await SaveAsync(containerName, blobName, content, overrideExisting, clientId);
    }

    /// <summary>Fails with <see cref="DriveErrorCodes.BlobNotFound"/> when there is no such file.</summary>
    public override async Task<IRemoteStreamContent?> GetOrNullAsync(string containerName, string blobName)
    {
        await CheckClientAsync(containerName);
        return await base.GetOrNullAsync(containerName, blobName)
               ?? throw new BusinessException(DriveErrorCodes.BlobNotFound)
                   .WithData("ContainerName", containerName)
                   .WithData("BlobName", blobName);
    }

    public override async Task<bool> ExistsAsync(string containerName, string blobName)
    {
        await CheckClientAsync(containerName);
        return await base.ExistsAsync(containerName, blobName);
    }

    public override async Task<bool> DeleteAsync(string containerName, string blobName)
    {
        await CheckClientAsync(containerName);
        return await base.DeleteAsync(containerName, blobName);
    }

    /// <summary>The calling client's id, once it may use <paramref name="containerName"/>.</summary>
    protected virtual async Task<string> CheckClientAsync(string containerName)
    {
        var clientId = CurrentClient.Id;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new AbpAuthorizationException("The Drive's BLOB API is for OAuth clients: the token names no client.");
        }

        if (CurrentUser.Id != null)
        {
            throw new AbpAuthorizationException(
                "The Drive's BLOB API refuses tokens that carry a user: an application calls it with its own client credentials, and a browser uses a ticket.");
        }

        if (!await ClientAuthorizer.IsAllowedAsync(containerName, clientId!))
        {
            throw new AbpAuthorizationException($"The client '{clientId}' may not use the Drive container '{containerName}'.");
        }

        return clientId!;
    }
}
