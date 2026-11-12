using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Applications;

[Collection(MongoTestCollection.Name)]
public class MongoDBBlobManagementBlobProvider_Tests : BlobManagementBlobProvider_Tests<BlobManagementMongoDbTestModule>
{
}
