using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Applications;

[Collection(MongoTestCollection.Name)]
public class MongoDBBlobManagementFiles_Tests : BlobManagementFiles_Tests<BlobManagementMongoDbTestModule>
{
}
