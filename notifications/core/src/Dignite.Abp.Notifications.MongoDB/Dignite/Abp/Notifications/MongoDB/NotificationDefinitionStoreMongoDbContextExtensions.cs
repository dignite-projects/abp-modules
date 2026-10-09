using MongoDB.Bson;
using MongoDB.Driver;
using Volo.Abp;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.Notifications.MongoDB;

public static class NotificationDefinitionStoreMongoDbContextExtensions
{
    /// <summary>
    /// Maps <c>NotifDefinitionGroups</c> and <c>NotifDefinitions</c> (prefix from
    /// <see cref="NotificationDefinitionStoreDbProperties"/>) with the indexes the EF Core tables have: a unique
    /// <c>Name</c> on both, and <c>GroupName</c> on the definitions. ABP's MongoDB model builder creates the collections
    /// and the indexes when a context first builds its model, so a host's own context that calls this gets them too.
    /// </summary>
    public static void ConfigureNotificationDefinitionStore(this IMongoModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<NotificationGroupDefinitionRecord>(b =>
        {
            b.CollectionName = NotificationDefinitionStoreDbProperties.DbTablePrefix + "DefinitionGroups";
            b.ConfigureIndexes(indexes =>
            {
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending(nameof(NotificationGroupDefinitionRecord.Name)),
                    new CreateIndexOptions { Unique = true }));
            });
        });

        builder.Entity<NotificationDefinitionRecord>(b =>
        {
            b.CollectionName = NotificationDefinitionStoreDbProperties.DbTablePrefix + "Definitions";
            b.ConfigureIndexes(indexes =>
            {
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending(nameof(NotificationDefinitionRecord.Name)),
                    new CreateIndexOptions { Unique = true }));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending(nameof(NotificationDefinitionRecord.GroupName))));
            });
        });
    }
}
