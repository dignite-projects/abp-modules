using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBDeletedFiles_Tests : DeletedFiles_Tests<BlobManagementMongoDbTestModule>
{
}
