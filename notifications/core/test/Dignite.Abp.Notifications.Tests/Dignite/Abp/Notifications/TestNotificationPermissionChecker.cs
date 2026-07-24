using System;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications;

/// <summary>Test double: grants every permission except <see cref="DeniedPermission"/>.</summary>
public class TestNotificationPermissionChecker : INotificationPermissionChecker
{
    public const string GrantedPermission = "Test.GrantedPermission";
    public const string DeniedPermission = "Test.DeniedPermission";

    public Task<bool> IsGrantedAsync(Guid userId, string permissionName)
    {
        return Task.FromResult(permissionName != DeniedPermission);
    }
}
