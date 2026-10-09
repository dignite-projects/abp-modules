using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications;

/// <summary>Default checker: every permission-gated notification is available to every user.</summary>
/// <remarks>
/// Registered with <c>TryRegister</c>: <c>Dignite.Abp.Notifications.Identity</c> replaces it without depending on this
/// package, so nothing orders the two modules, and a plain registration made after the real checker would silently
/// grant every permission.
/// </remarks>
[Dependency(TryRegister = true)]
public class AlwaysGrantedNotificationPermissionChecker : INotificationPermissionChecker, ISingletonDependency
{
    public Task<bool> IsGrantedAsync(Guid userId, string permissionName)
    {
        return Task.FromResult(true);
    }
}
