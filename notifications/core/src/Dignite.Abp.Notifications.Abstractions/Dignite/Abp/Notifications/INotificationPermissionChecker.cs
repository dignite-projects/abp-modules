using System;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Decides whether a permission-gated notification is available to a specific user. The default implementation
/// grants everything; the optional Identity integration replaces it with a real ABP authorization check.
/// </summary>
/// <remarks>
/// <b>Tenant contract</b>: implementations must not switch tenants — evaluate against the ambient one. Distribution
/// recipient eligibility switches to the notification's tenant before calling the definition manager. The ambient
/// tenant reaches this service even though <see cref="NotificationDefinitionManager"/> resolves it from a fresh DI
/// scope, because ABP's <c>ICurrentTenantAccessor</c> is an AsyncLocal singleton.
/// </remarks>
public interface INotificationPermissionChecker
{
    /// <param name="userId">The user the notification would be delivered to.</param>
    /// <param name="permissionName">The permission declared by the notification definition.</param>
    Task<bool> IsGrantedAsync(Guid userId, string permissionName);
}
