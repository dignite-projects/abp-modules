using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Authorization.Permissions.Resources;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;

namespace Dignite.Abp.BlobManagement;

/// <summary>Signing in as a test user, and granting through ABP's permission managers as its dialogs do.</summary>
public static class BlobManagementTestGrants
{
    public static IDisposable As(this ICurrentPrincipalAccessor principalAccessor, Guid userId, params string[] roles)
    {
        var claims = new List<Claim> { new(AbpClaimTypes.UserId, userId.ToString()) };
        claims.AddRange(roles.Select(role => new Claim(AbpClaimTypes.Role, role)));
        return principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
    }

    /// <summary>Calling as an OAuth client acting for itself: a token that names a client and carries no user.</summary>
    public static IDisposable AsClient(this ICurrentPrincipalAccessor principalAccessor, string clientId)
    {
        return principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(AbpClaimTypes.ClientId, clientId) }, "Test")));
    }

    /// <summary>Grants module-wide permissions - reading, managing grants - to a user.</summary>
    public static async Task GrantAsync(this IPermissionManager permissionManager, Guid userId, params string[] permissions)
    {
        foreach (var permission in permissions)
        {
            await permissionManager.SetAsync(permission, UserPermissionValueProvider.ProviderName, userId.ToString(), true);
        }
    }

    /// <summary>Grants permissions on one container to a user.</summary>
    public static Task GrantOnContainerAsync(
        this IResourcePermissionManager manager,
        string containerName,
        Guid userId,
        params string[] permissions)
    {
        return manager.GrantOnAsync(BlobManagementResourceNames.Container, containerName, userId, permissions);
    }

    /// <summary>
    /// Grants the files of one container to an OAuth client: ABP's resource permission for the client provider
    /// (<c>C</c>), as ABP's resource permission dialog grants it to a client.
    /// </summary>
    public static Task GrantFilesToClientAsync(this IResourcePermissionManager manager, string containerName, string clientId)
    {
        return manager.SetAsync(
            BlobManagementPermissions.Container.Files,
            BlobManagementResourceNames.Container,
            containerName,
            ClientResourcePermissionValueProvider.ProviderName,
            clientId,
            true);
    }

    private static async Task GrantOnAsync(
        this IResourcePermissionManager manager,
        string resourceName,
        string resourceKey,
        Guid userId,
        string[] permissions)
    {
        foreach (var permission in permissions)
        {
            await manager.SetAsync(
                permission, resourceName, resourceKey, UserResourcePermissionValueProvider.ProviderName, userId.ToString(), true);
        }
    }
}
