using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBBlobManagementContainerSettings_Tests : BlobManagementContainerSettings_Tests<BlobManagementMongoDbTestModule>
{
}
