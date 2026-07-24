using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace Dignite.Abp.Notifications.Identity;

/// <summary>
/// Real permission check backed by ABP Identity + authorization. Resolves the target user, builds their claims
/// principal (roles + granted claims), and asks the permission checker. Registered transient and resolved from a
/// fresh scope per call by the singleton definition manager, so no request-scoped service is captured (roadmap B).
/// Does not switch tenants — see the tenant contract on <see cref="INotificationPermissionChecker"/>.
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(INotificationPermissionChecker))]
public class IdentityNotificationPermissionChecker : INotificationPermissionChecker, ITransientDependency
{
    protected IIdentityUserRepository UserRepository { get; }

    protected IUserClaimsPrincipalFactory<IdentityUser> PrincipalFactory { get; }

    protected IPermissionChecker PermissionChecker { get; }

    public IdentityNotificationPermissionChecker(
        IIdentityUserRepository userRepository,
        IUserClaimsPrincipalFactory<IdentityUser> principalFactory,
        IPermissionChecker permissionChecker)
    {
        UserRepository = userRepository;
        PrincipalFactory = principalFactory;
        PermissionChecker = permissionChecker;
    }

    public virtual async Task<bool> IsGrantedAsync(Guid userId, string permissionName)
    {
        var user = await UserRepository.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        var principal = await PrincipalFactory.CreateAsync(user);
        return await PermissionChecker.IsGrantedAsync(principal, permissionName);
    }
}
