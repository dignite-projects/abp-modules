using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBBlobManagementLocalization_Tests : BlobManagementLocalization_Tests<BlobManagementMongoDbTestModule>
{
}
