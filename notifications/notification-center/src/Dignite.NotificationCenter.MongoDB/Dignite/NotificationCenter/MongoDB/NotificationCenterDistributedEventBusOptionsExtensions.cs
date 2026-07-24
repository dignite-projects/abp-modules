using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MongoDB.DistributedEvents;

namespace Dignite.NotificationCenter.MongoDB;

/// <summary>
/// Configuration helpers for routing ABP distributed event boxes through Notification Center MongoDB.
/// </summary>
public static class NotificationCenterDistributedEventBusOptionsExtensions
{
    /// <summary>
    /// Routes ABP's distributed event outbox and inbox through
    /// <see cref="NotificationCenterMongoDbContext"/>. Application startup validates that the selected
    /// MongoDB deployment supports multi-document transactions before exposing an atomicity guarantee.
    /// </summary>
    public static void UseNotificationCenterMongoDbOutbox(this AbpDistributedEventBusOptions options)
    {
        options.Outboxes.Configure(config => config.UseMongoDbContext<NotificationCenterMongoDbContext>());
        options.Inboxes.Configure(config => config.UseMongoDbContext<NotificationCenterMongoDbContext>());
    }
}
