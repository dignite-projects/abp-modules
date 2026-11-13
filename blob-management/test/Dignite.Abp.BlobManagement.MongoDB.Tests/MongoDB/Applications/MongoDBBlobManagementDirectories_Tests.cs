using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Applications;

[Collection(MongoTestCollection.Name)]
public class MongoDBBlobManagementDirectories_Tests : BlobManagementDirectories_Tests<BlobManagementMongoDbTestModule>
{
}
