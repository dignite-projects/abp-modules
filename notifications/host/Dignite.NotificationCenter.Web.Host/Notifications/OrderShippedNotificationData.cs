using Dignite.Abp.Notifications;

namespace Dignite.NotificationCenter.Web.Host.Notifications;

/// <summary>
/// A demo notification payload standing in for one a real business module would define. Implements
/// <see cref="IHasNotificationImageUrl"/> so the default bell card shows its image, and carries an order
/// number the demo's custom ViewComponent + entity-link resolver key off — exercising all three extension
/// points end-to-end (image card, per-discriminator custom renderer, entity click-through).
/// </summary>
[NotificationDataType("Demo.OrderShipped")]
public class OrderShippedNotificationData : NotificationData, IHasNotificationImageUrl
{
    public string OrderNumber { get; set; } = default!;

    public int ItemCount { get; set; }

    public string? ImageUrl { get; set; }
}
