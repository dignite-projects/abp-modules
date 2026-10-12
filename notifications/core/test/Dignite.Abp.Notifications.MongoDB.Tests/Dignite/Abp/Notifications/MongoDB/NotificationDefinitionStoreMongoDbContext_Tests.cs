using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace Dignite.Abp.Notifications.MongoDB;

/// <summary>
/// What the MongoDB provider leaves in the database, read with a plain driver client: the two collections under the
/// store's prefix, and the indexes the EF Core tables have.
/// </summary>
[Collection(MongoTestCollection.Name)]
public class NotificationDefinitionStoreMongoDbContext_Tests
{
    [Fact]
    public async Task The_collections_carry_the_store_prefix_and_the_EF_Core_indexes()
    {
        using var shared = new MongoDbDefinitionStoreInfrastructure();
        await using (var publisherA =
                     await DefinitionStoreTestApplication.StartAsync<PublisherATestModule>("PublisherA", shared))
        {
            await publisherA.SaveStaticDefinitionsAsync();
        }

        var database = new MongoClient(shared.ConnectionString)
            .GetDatabase(MongoUrl.Create(shared.ConnectionString).DatabaseName);

        (await (await database.ListCollectionNamesAsync()).ToListAsync())
            .ShouldBe(new[] { "NotifDefinitionGroups", "NotifDefinitions" }, ignoreOrder: true);

        (await GetIndexesAsync(database, "NotifDefinitionGroups")).ShouldBe(new[]
        {
            ("Name", true),
            ("_id", false)
        }, ignoreOrder: true);

        (await GetIndexesAsync(database, "NotifDefinitions")).ShouldBe(new[]
        {
            ("GroupName", false),
            ("Name", true),
            ("_id", false)
        }, ignoreOrder: true);
    }

    /// <summary>Each index as its single key field and whether it is unique.</summary>
    private static async Task<List<(string Key, bool Unique)>> GetIndexesAsync(IMongoDatabase database, string collection)
    {
        var indexes = await (await database.GetCollection<BsonDocument>(collection).Indexes.ListAsync()).ToListAsync();

        return indexes
            .Select(index => (
                index["key"].AsBsonDocument.Names.Single(),
                index.GetValue("unique", false).ToBoolean()))
            .ToList();
    }
}
