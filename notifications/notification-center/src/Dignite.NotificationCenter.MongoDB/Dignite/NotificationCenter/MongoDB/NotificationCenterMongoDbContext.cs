using MongoDB.Bson;
using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;
using Volo.Abp.MongoDB.DistributedEvents;

namespace Dignite.NotificationCenter.MongoDB;

[ConnectionStringName(NotificationCenterDbProperties.ConnectionStringName)]
public class NotificationCenterMongoDbContext : AbpMongoDbContext, INotificationCenterMongoDbContext
{
    public IMongoCollection<Notification> Notifications => Collection<Notification>();

    public IMongoCollection<UserNotification> UserNotifications => Collection<UserNotification>();

    public IMongoCollection<NotificationSubscription> NotificationSubscriptions => Collection<NotificationSubscription>();

    public IMongoCollection<IncomingEventRecord> IncomingEvents => Collection<IncomingEventRecord>();

    public IMongoCollection<OutgoingEventRecord> OutgoingEvents => Collection<OutgoingEventRecord>();

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);

        modelBuilder.ConfigureEventInbox();
        modelBuilder.ConfigureEventOutbox();

        // ABP's MongoDB model builder supplies the conventional collection names but does not add the
        // query indexes that its EF Core model supplies. Keep both providers aligned with the sender,
        // processor, duplicate-detection, and cleanup query shapes.
        modelBuilder.Entity<IncomingEventRecord>(b =>
        {
            b.ConfigureIndexes(indexes =>
            {
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(IncomingEventRecord.Status))
                        .Ascending(nameof(IncomingEventRecord.CreationTime))));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(IncomingEventRecord.MessageId)),
                    new CreateIndexOptions { Unique = true }));
            });
        });

        modelBuilder.Entity<OutgoingEventRecord>(b =>
        {
            b.ConfigureIndexes(indexes =>
            {
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(OutgoingEventRecord.CreationTime))));
            });
        });

        modelBuilder.Entity<Notification>(b =>
        {
            b.CollectionName = NotificationCenterDbProperties.DbTablePrefix + "Notifications";
            b.ConfigureIndexes(indexes =>
            {
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(Notification.TenantId))
                        .Ascending(nameof(Notification.NotificationName))
                        .Descending(nameof(Notification.CreationTime))));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(Notification.TenantId))
                        .Ascending(nameof(Notification.CreationTime))));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(Notification.CreationTime))));
            });
        });

        modelBuilder.Entity<UserNotification>(b =>
        {
            b.CollectionName = NotificationCenterDbProperties.DbTablePrefix + "UserNotifications";
            b.ConfigureIndexes(indexes =>
            {
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(UserNotification.TenantId))
                        .Ascending(nameof(UserNotification.UserId))
                        .Ascending(nameof(UserNotification.State))
                        .Descending(nameof(UserNotification.CreationTime))));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(UserNotification.TenantId))
                        .Ascending(nameof(UserNotification.State))
                        .Ascending(nameof(UserNotification.CreationTime))));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(UserNotification.State))
                        .Ascending(nameof(UserNotification.CreationTime))));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(UserNotification.TenantId))
                        .Ascending(nameof(UserNotification.NotificationId))));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(UserNotification.UserId))
                        .Ascending(nameof(UserNotification.NotificationId)),
                    new CreateIndexOptions { Unique = true }));
            });
        });

        modelBuilder.Entity<NotificationSubscription>(b =>
        {
            b.CollectionName = NotificationCenterDbProperties.DbTablePrefix + "NotificationSubscriptions";
            b.ConfigureIndexes(indexes =>
            {
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(NotificationSubscription.TenantKey))
                        .Ascending(nameof(NotificationSubscription.NotificationNameKey))
                        .Ascending(nameof(NotificationSubscription.ScopeKey))));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(NotificationSubscription.TenantKey))
                        .Ascending(nameof(NotificationSubscription.UserId))
                        .Ascending(nameof(NotificationSubscription.NotificationNameKey))
                        .Ascending(nameof(NotificationSubscription.ScopeKey)),
                    new CreateIndexOptions { Unique = true }));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys
                        .Ascending(nameof(NotificationSubscription.TenantKey))
                        .Ascending(nameof(NotificationSubscription.UserId))
                        .Ascending(nameof(NotificationSubscription.NotificationNameKey))));
            });
        });
    }
}
