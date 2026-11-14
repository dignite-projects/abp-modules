using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Applications;

[Collection(MongoTestCollection.Name)]
public class MongoDBBlobManagementPermissionDialog_Tests : BlobManagementPermissionDialog_Tests<BlobManagementMongoDbTestModule>
{
}
