using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver.Linq;
using Volo.Abp.Domain.Repositories.MongoDB;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.Notifications.MongoDB;

public class MongoNotificationDefinitionRecordRepository :
    MongoDbRepository<INotificationDefinitionStoreMongoDbContext, NotificationDefinitionRecord, Guid>,
    INotificationDefinitionRecordRepository
{
    public MongoNotificationDefinitionRecordRepository(
        IMongoDbContextProvider<INotificationDefinitionStoreMongoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<NotificationDefinitionRecord?> FindByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        cancellationToken = GetCancellationToken(cancellationToken);
        return await (await GetQueryableAsync(cancellationToken))
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(record => record.Name == name, cancellationToken);
    }
}
