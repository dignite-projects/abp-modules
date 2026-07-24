using System;
using System.Threading.Tasks;
using Dignite.NotificationCenter.Web.Host.Notifications;
using Dignite.Abp.Notifications;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Users;

namespace Dignite.NotificationCenter.Web.Host.Services;

/// <summary>
/// Demo-only helpers so you can trigger notifications from the UI. Auto-exposed by ConventionalControllers as
/// POST /api/app/demo-notification/publish-test, /publish-order-shipped, and
/// /publish-announcement-to-subscribers.
/// </summary>
[Authorize]
public class DemoNotificationAppService : HostAppService
{
    private readonly INotificationPublisher _publisher;

    public DemoNotificationAppService(INotificationPublisher publisher)
    {
        _publisher = publisher;
    }

    /// <summary>Always delivered to the caller, regardless of subscription state — so you can watch the bell
    /// update live over SignalR. Explicit userIds bypass subscriptions entirely (see DefaultNotificationDistributor).</summary>
    public virtual Task PublishTestAsync()
    {
        return _publisher.PublishAsync(
            "Demo.Announcement",
            new MessageNotificationData($"Test notification at {Clock.Now:T}"),
            severity: NotificationSeverity.Info,
            userIds: new[] { CurrentUser.GetId() });
    }

    /// <summary>
    /// Publishes the demo order-shipped payload to the caller so Angular can exercise a custom
    /// NotificationData renderer instead of only the built-in text renderer.
    /// </summary>
    public virtual Task PublishOrderShippedAsync()
    {
        var orderId = Clock.Now.ToString("HHmmss");

        return _publisher.PublishAsync(
            "Demo.OrderShipped",
            new OrderShippedNotificationData
            {
                OrderNumber = $"SO-{orderId}",
                ItemCount = 2,
                ImageUrl = "https://placehold.co/60x60"
            },
            new NotificationEntityIdentifier("Demo.Order", orderId),
            NotificationSeverity.Success,
            new[] { CurrentUser.GetId() });
    }

    /// <summary>
    /// Demonstrates subscription-driven delivery: no userIds is passed, so DefaultNotificationDistributor
    /// resolves recipients from NotificationSubscription rows instead — only users currently subscribed to
    /// "Demo.Announcement" receive this (see Subscriptions page / nc-notification-subscriptions). Unsubscribe
    /// and publish again to see it NOT arrive.
    /// </summary>
    public virtual Task PublishAnnouncementToSubscribersAsync()
    {
        return _publisher.PublishAsync(
            "Demo.Announcement",
            new MessageNotificationData($"Broadcast to subscribers at {Clock.Now:T}"),
            severity: NotificationSeverity.Info);
    }
}
