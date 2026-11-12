using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBUserBlobManagementFiles_Tests : UserBlobManagementFiles_Tests<BlobManagementMongoDbTestModule>
{
}
