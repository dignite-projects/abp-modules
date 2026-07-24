using System.Threading.Tasks;
using Dignite.NotificationCenter.MongoDB;
using MongoDB.Driver;
using Shouldly;
using Volo.Abp.MongoDB;
using Volo.Abp.MongoDB.DistributedEvents;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>Runs the transactional event-box contract on real topologies.</summary>
[Collection(MongoTestCollection.Name)]
public class Notification_Outbox_Tests :
    Notification_Outbox_Tests<NotificationCenterMongoDbOutboxTestModule>
{
    protected override async Task<long> GetOutboxCountAsync()
    {
        var dbContext = await GetRequiredService<IMongoDbContextProvider<NotificationCenterMongoDbContext>>()
            .GetDbContextAsync();

        if (dbContext.SessionHandle != null)
        {
            return await dbContext.OutgoingEvents.CountDocumentsAsync(
                dbContext.SessionHandle,
                Builders<OutgoingEventRecord>.Filter.Empty);
        }

        return await dbContext.OutgoingEvents.CountDocumentsAsync(Builders<OutgoingEventRecord>.Filter.Empty);
    }

    protected override async Task AssertProviderTransactionActiveAsync()
    {
        var dbContext = await GetRequiredService<IMongoDbContextProvider<NotificationCenterMongoDbContext>>()
            .GetDbContextAsync();
        dbContext.SessionHandle.ShouldNotBeNull();
        dbContext.SessionHandle!.IsInTransaction.ShouldBeTrue();
    }
}
