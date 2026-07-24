using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;
using Volo.Abp.MongoDB.DistributedEvents;

namespace Dignite.NotificationCenter.MongoDB;

[ConnectionStringName(NotificationCenterDbProperties.ConnectionStringName)]
public interface INotificationCenterMongoDbContext :
    IAbpMongoDbContext,
    IHasEventInbox,
    IHasEventOutbox
{
    IMongoCollection<Notification> Notifications { get; }

    IMongoCollection<UserNotification> UserNotifications { get; }

    IMongoCollection<NotificationSubscription> NotificationSubscriptions { get; }
}
