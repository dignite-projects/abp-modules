using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Permissions;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions.Resources;
using Volo.Abp.BlobStoring;
using Volo.Abp.Clients;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// The authorization of the integration entry's remote callers, shared by the BLOB API and the file API (both
/// application services call it; nothing in the domain does):
/// <list type="bullet">
/// <item>The caller must be an OAuth client (a <c>client_id</c> claim).</item>
/// <item>A token that carries a user is refused, whatever its client: a user token carries <c>client_id</c> too, and
/// ABP's client permission provider reads nothing else, so a client granted on a container would otherwise let its
/// users in. A user's request reaches Blob Management through the application, which applies its own rules first.</item>
/// <item>The client must hold <see cref="BlobManagementPermissions.Container.Files"/> on the container, a managed one:
/// ABP's resource permission granted to the client (provider <c>C</c>), which ABP's resource permission checker
/// reads.</item>
/// </list>
/// </summary>
public class BlobManagementClientChecker : ITransientDependency
{
    protected ICurrentClient CurrentClient { get; }

    protected ICurrentUser CurrentUser { get; }

    protected IResourcePermissionChecker ResourcePermissionChecker { get; }

    protected IBlobContainerConfigurationProvider ContainerConfigurationProvider { get; }

    public BlobManagementClientChecker(
        ICurrentClient currentClient,
        ICurrentUser currentUser,
        IResourcePermissionChecker resourcePermissionChecker,
        IBlobContainerConfigurationProvider containerConfigurationProvider)
    {
        CurrentClient = currentClient;
        CurrentUser = currentUser;
        ResourcePermissionChecker = resourcePermissionChecker;
        ContainerConfigurationProvider = containerConfigurationProvider;
    }

    /// <summary>The calling client's id, once it may use <paramref name="containerName"/>; throws <see cref="AbpAuthorizationException"/> otherwise.</summary>
    public virtual async Task<string> CheckAsync(string containerName)
    {
        var clientId = CheckIsClient();
        if (!await IsAllowedAsync(containerName))
        {
            throw new AbpAuthorizationException($"The client '{clientId}' may not use the managed container '{containerName}'.");
        }

        return clientId;
    }

    /// <summary>
    /// The calling client's id, when the caller is an OAuth client acting for itself; throws
    /// <see cref="AbpAuthorizationException"/> otherwise. Whether it may use a container is still to be asked
    /// (<see cref="IsAllowedAsync"/>).
    /// </summary>
    public virtual string CheckIsClient()
    {
        var clientId = CurrentClient.Id;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new AbpAuthorizationException("Blob Management's integration API is for OAuth clients: the token names no client.");
        }

        if (CurrentUser.Id != null)
        {
            throw new AbpAuthorizationException(
                "Blob Management's integration API refuses tokens that carry a user: an application calls it with its own client credentials, for its user.");
        }

        return clientId!;
    }

    /// <summary>
    /// Whether the calling client - checked with <see cref="CheckIsClient"/> first - may use
    /// <paramref name="containerName"/>: a managed container, on which it holds
    /// <see cref="BlobManagementPermissions.Container.Files"/>. A container the host never declared allows no client,
    /// whatever was granted under its name.
    /// </summary>
    public virtual async Task<bool> IsAllowedAsync(string containerName)
    {
        return ContainerConfigurationProvider.IsManagedContainer(containerName) &&
               await ResourcePermissionChecker.IsGrantedAsync(
                   BlobManagementPermissions.Container.Files,
                   BlobManagementResourceNames.Container,
                   containerName);
    }
}
