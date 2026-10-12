using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications;

/// <summary>Default checker: every permission-gated notification is available to every user.</summary>
/// <remarks>
/// The default of <see cref="INotificationPermissionChecker"/>, next to the contract as ABP keeps
/// <c>AlwaysAllowPermissionChecker</c> next to <c>IPermissionChecker</c>. Registered with <c>TryRegister</c>, so whatever
/// registered a checker first keeps it; <c>Dignite.Abp.Notifications.Identity</c> replaces it.
/// </remarks>
[Dependency(TryRegister = true)]
public class AlwaysGrantedNotificationPermissionChecker : INotificationPermissionChecker, ISingletonDependency
{
    public Task<bool> IsGrantedAsync(Guid userId, string permissionName)
    {
        return Task.FromResult(true);
    }
}
