using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Applications;

[Collection(MongoTestCollection.Name)]
public class MongoDBBlobManagementUploadTickets_Tests : BlobManagementUploadTickets_Tests<BlobManagementMongoDbTestModule>
{
}
