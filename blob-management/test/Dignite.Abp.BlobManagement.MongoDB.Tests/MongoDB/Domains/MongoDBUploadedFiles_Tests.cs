using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBUploadedFiles_Tests : UploadedFiles_Tests<BlobManagementMongoDbTestModule>
{
}
