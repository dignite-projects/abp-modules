using System;
using Volo.Abp;

namespace Dignite.Abp.Notifications.Emailing;

/// <summary>
/// Context used by email address resolvers. Tenant information is explicit so local repository-backed
/// resolvers and remote/microservice-backed resolvers can handle it in their own boundary.
/// </summary>
public class EmailNotificationAddressResolveContext
{
    public NotificationPayload Notification { get; }

    public Guid UserId { get; }

    /// <summary>
    /// The tenant that owns the notification, carried over from the delivery request. A local
    /// repository-backed resolver does not need it — ABP's event bus has already entered this tenant before the
    /// notifier runs. It is exposed for resolvers that must forward the tenant across a boundary the ambient scope
    /// cannot cross, such as a remote user service.
    /// </summary>
    public Guid? TenantId { get; }

    public EmailNotificationAddressResolveContext(
        NotificationPayload notification,
        Guid userId,
        Guid? tenantId)
    {
        Notification = Check.NotNull(notification, nameof(notification));
        UserId = userId;
        TenantId = tenantId;
    }
}

