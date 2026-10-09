using System;
using Volo.Abp.Domain.Repositories.MongoDB;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.Notifications.MongoDB;

public class MongoNotificationGroupDefinitionRecordRepository :
    MongoDbRepository<INotificationDefinitionStoreMongoDbContext, NotificationGroupDefinitionRecord, Guid>,
    INotificationGroupDefinitionRecordRepository
{
    public MongoNotificationGroupDefinitionRecordRepository(
        IMongoDbContextProvider<INotificationDefinitionStoreMongoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
