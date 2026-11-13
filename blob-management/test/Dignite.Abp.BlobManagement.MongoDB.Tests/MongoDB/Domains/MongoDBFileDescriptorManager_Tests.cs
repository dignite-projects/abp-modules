using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBFileDescriptorManager_Tests : FileDescriptorManager_Tests<BlobManagementMongoDbTestModule>
{
}
