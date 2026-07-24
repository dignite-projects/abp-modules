using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.SignalR;

namespace Dignite.Abp.Notifications.SignalR;

/// <summary>
/// SignalR hub clients connect to for real-time notifications. Delivery is push-only (server → client);
/// clients invoke no server methods here.
///
/// ABP auto-registers this hub (transient) and auto-maps its endpoint to <c>/signalr-hubs/notifications</c>
/// (the conventional route for <c>NotificationsHub</c>) — a host MUST NOT also call <c>MapHub</c> for it, or the
/// endpoint is registered twice and SignalR's negotiate fails with <c>AmbiguousMatchException</c> (HTTP 500).
///
/// <see cref="AuthorizeAttribute"/>: only authenticated users connect. Delivery is per-user — the notifier
/// pushes via <c>Clients.Users(userIds)</c>, resolved through ABP's <c>AbpSignalRUserIdProvider</c> (ICurrentUser).
/// </summary>
[Authorize]
public class NotificationsHub : AbpHub<INotificationsClient>
{
}
