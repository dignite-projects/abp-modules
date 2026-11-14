using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Applications;

[Collection(MongoTestCollection.Name)]
public class MongoDBBlobManagementReadLinks_Tests : BlobManagementReadLinks_Tests<BlobManagementMongoDbTestModule>
{
}
