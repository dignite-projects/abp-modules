using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
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

    /// <summary>Grants module-wide permissions to a user.</summary>
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

    /// <summary>Grants permissions on one directory to a user.</summary>
    public static Task GrantOnDirectoryAsync(
        this IResourcePermissionManager manager,
        Guid directoryId,
        Guid userId,
        params string[] permissions)
    {
        return manager.GrantOnAsync(BlobManagementResourceNames.Directory, directoryId.ToString(), userId, permissions);
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
