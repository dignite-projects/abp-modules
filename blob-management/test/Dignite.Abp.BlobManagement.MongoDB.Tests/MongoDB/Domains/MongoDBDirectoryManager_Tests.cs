using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBDirectoryManager_Tests : DirectoryManager_Tests<BlobManagementMongoDbTestModule>
{
}
