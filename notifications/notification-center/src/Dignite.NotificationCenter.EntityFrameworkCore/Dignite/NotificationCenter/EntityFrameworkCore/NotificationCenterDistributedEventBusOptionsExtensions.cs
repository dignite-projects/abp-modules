using Volo.Abp.EntityFrameworkCore.DistributedEvents;
using Volo.Abp.EventBus.Distributed;

namespace Dignite.NotificationCenter.EntityFrameworkCore;

public static class NotificationCenterDistributedEventBusOptionsExtensions
{
    /// <summary>
    /// Routes ABP's distributed event outbox/inbox through <see cref="NotificationCenterDbContext"/>, making
    /// notification persistence and <see cref="Dignite.Abp.Notifications.NotificationDeliveryRequestedEto"/> publication atomic
    /// when the host enables ABP's transactional outbox.
    /// </summary>
    public static void UseNotificationCenterEfCoreOutbox(this AbpDistributedEventBusOptions options)
    {
        options.Outboxes.Configure(config => config.UseDbContext<NotificationCenterDbContext>());
        options.Inboxes.Configure(config => config.UseDbContext<NotificationCenterDbContext>());
    }
}
