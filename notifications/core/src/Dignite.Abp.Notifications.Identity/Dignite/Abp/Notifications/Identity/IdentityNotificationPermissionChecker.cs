using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;

namespace Dignite.Abp.Notifications.Identity;

/// <summary>
/// Real permission check backed by ABP authorization. Asks <see cref="IUserRoleFinder"/> for the user's roles, builds
/// the minimal claims principal ABP's permission providers read (<see cref="AbpClaimTypes.UserId"/> for the user
/// provider, every <see cref="AbpClaimTypes.Role"/> for the role provider, and <see cref="AbpClaimTypes.TenantId"/> for
/// the multi-tenancy side), and asks the permission checker. It needs no Identity database: the role finder is the
/// in-process <c>UserRoleFinder</c> in a monolith and an HTTP client in a split deployment. Registered transient and
/// resolved from a fresh scope per call by the singleton definition manager, so no request-scoped service is captured
/// (roadmap B). Does not switch tenants — see the tenant contract on <see cref="INotificationPermissionChecker"/>.
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(INotificationPermissionChecker))]
public class IdentityNotificationPermissionChecker : INotificationPermissionChecker, ITransientDependency
{
    /// <summary>Marks the principal as authenticated; the value itself is not read by any ABP permission provider.</summary>
    protected const string AuthenticationType = "Dignite.Abp.Notifications";

    protected IUserRoleFinder UserRoleFinder { get; }

    protected IPermissionChecker PermissionChecker { get; }

    protected ICurrentTenant CurrentTenant { get; }

    public IdentityNotificationPermissionChecker(
        IUserRoleFinder userRoleFinder,
        IPermissionChecker permissionChecker,
        ICurrentTenant currentTenant)
    {
        UserRoleFinder = userRoleFinder;
        PermissionChecker = permissionChecker;
        CurrentTenant = currentTenant;
    }

    public virtual async Task<bool> IsGrantedAsync(Guid userId, string permissionName)
    {
        var roleNames = await UserRoleFinder.GetRoleNamesAsync(userId);
        var principal = CreatePrincipal(userId, roleNames);
        return await PermissionChecker.IsGrantedAsync(principal, permissionName);
    }

    /// <summary>
    /// The user's id, one role claim per role, and the ambient tenant. The distributor has already switched to the
    /// notification's tenant, so the host side (no tenant) carries no tenant claim.
    /// </summary>
    protected virtual ClaimsPrincipal CreatePrincipal(Guid userId, IEnumerable<string> roleNames)
    {
        var claims = new List<Claim> { new(AbpClaimTypes.UserId, userId.ToString()) };

        foreach (var roleName in roleNames)
        {
            claims.Add(new Claim(AbpClaimTypes.Role, roleName));
        }

        if (CurrentTenant.Id.HasValue)
        {
            claims.Add(new Claim(AbpClaimTypes.TenantId, CurrentTenant.Id.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType));
    }
}
