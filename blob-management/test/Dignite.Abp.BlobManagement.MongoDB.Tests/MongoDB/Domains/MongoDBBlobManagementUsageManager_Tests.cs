using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBBlobManagementUsageManager_Tests : BlobManagementUsageManager_Tests<BlobManagementMongoDbTestModule>
{
}
