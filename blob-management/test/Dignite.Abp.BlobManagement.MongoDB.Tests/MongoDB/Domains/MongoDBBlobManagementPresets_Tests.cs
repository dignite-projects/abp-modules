using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBBlobManagementPresets_Tests : BlobManagementPresets_Tests<BlobManagementMongoDbTestModule>
{
}
