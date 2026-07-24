using System;
using System.Collections.Generic;

namespace Dignite.NotificationCenter.Web;

/// <summary>
/// Extension points for the notification bell/list UI. Both dictionaries are opt-in: an item with no
/// matching entry falls back to the default, generic card (see <c>Bell/Default.cshtml</c>).
/// </summary>
public class NotificationCenterWebOptions
{
    /// <summary>
    /// Root-relative path or absolute URL the client-side bell connects to for real-time notifications over
    /// SignalR. The default matches ABP's automatic hub route for <c>NotificationsHub</c>; hosts should not map
    /// the hub manually. Remote MVC deployments can point this at the externally reachable hub URL while the
    /// Notification Center REST API remains the source of truth. When the <c>Dignite.Abp.Notifications.SignalR</c>
    /// notifier isn't installed (or <c>@microsoft/signalr</c> isn't loaded), the bell degrades gracefully to a
    /// non-live view.
    /// </summary>
    public string SignalRHubUrl { get; set; } = "/signalr-hubs/notifications";

    /// <summary>
    /// Custom rendering for a specific notification data type, keyed by its stable
    /// <see cref="Dignite.Abp.Notifications.NotificationDataTypeAttribute"/> discriminator — never a CLR type
    /// name. The registered <see cref="Microsoft.AspNetCore.Mvc.ViewComponent"/> is invoked with a single
    /// <c>data</c> argument holding the item's <see cref="Dignite.Abp.Notifications.NotificationData"/>.
    /// </summary>
    public Dictionary<string, Type> DataViewComponents { get; } = new();

    /// <summary>
    /// Resolves a clickable URL for a <see cref="UserNotificationDto"/>, keyed by its
    /// <see cref="UserNotificationDto.EntityTypeName"/>. Each resolver maps an <c>EntityId</c> to a URL in
    /// this host's own routing scheme — deliberately not a single URL carried on the notification itself,
    /// since different UIs (this MVC host vs. an Angular SPA) resolve the same entity to different routes.
    /// </summary>
    public Dictionary<string, Func<string, string>> EntityLinkResolvers { get; } = new();
}
