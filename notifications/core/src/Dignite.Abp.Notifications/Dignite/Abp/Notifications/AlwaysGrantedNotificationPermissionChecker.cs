using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications;

/// <summary>Default checker: every permission-gated notification is available to every user.</summary>
public class AlwaysGrantedNotificationPermissionChecker : INotificationPermissionChecker, ISingletonDependency
{
    public Task<bool> IsGrantedAsync(Guid userId, string permissionName)
    {
        return Task.FromResult(true);
    }
}
