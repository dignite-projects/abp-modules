using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBDirectoryPathImporter_Tests : DirectoryPathImporter_Tests<BlobManagementMongoDbTestModule>
{
}
