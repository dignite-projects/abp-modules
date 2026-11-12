using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.Clients;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// The authorization of the integration entry's remote callers, shared by the BLOB API and ticket issuing (both
/// application services call it; nothing in the domain does):
/// <list type="bullet">
/// <item>The caller must be an OAuth client (a <c>client_id</c> claim).</item>
/// <item>A token that carries a user is refused, whatever its client: a user token carries <c>client_id</c> too, so an
/// allowed first-party client would otherwise let its users in. Browsers reach Blob Management through tickets.</item>
/// <item>The client must be allowed in the container (<see cref="IBlobManagementClientAuthorizer"/>).</item>
/// </list>
/// </summary>
public class BlobManagementClientChecker : ITransientDependency
{
    protected ICurrentClient CurrentClient { get; }

    protected ICurrentUser CurrentUser { get; }

    protected IBlobManagementClientAuthorizer ClientAuthorizer { get; }

    public BlobManagementClientChecker(ICurrentClient currentClient, ICurrentUser currentUser, IBlobManagementClientAuthorizer clientAuthorizer)
    {
        CurrentClient = currentClient;
        CurrentUser = currentUser;
        ClientAuthorizer = clientAuthorizer;
    }

    /// <summary>The calling client's id, once it may use <paramref name="containerName"/>; throws <see cref="AbpAuthorizationException"/> otherwise.</summary>
    public virtual async Task<string> CheckAsync(string containerName)
    {
        var clientId = CurrentClient.Id;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new AbpAuthorizationException("The BlobManagement's integration API is for OAuth clients: the token names no client.");
        }

        if (CurrentUser.Id != null)
        {
            throw new AbpAuthorizationException(
                "The BlobManagement's integration API refuses tokens that carry a user: an application calls it with its own client credentials, and a browser uses a ticket.");
        }

        if (!await ClientAuthorizer.IsAllowedAsync(containerName, clientId!))
        {
            throw new AbpAuthorizationException($"The client '{clientId}' may not use the BlobManagement container '{containerName}'.");
        }

        return clientId!;
    }
}
