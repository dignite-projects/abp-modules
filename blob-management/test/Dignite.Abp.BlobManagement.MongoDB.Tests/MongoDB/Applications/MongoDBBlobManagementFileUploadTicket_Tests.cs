using Xunit;

namespace Dignite.Abp.BlobManagement.MongoDB.Applications;

[Collection(MongoTestCollection.Name)]
public class MongoDBBlobManagementFileUploadTicket_Tests : BlobManagementFileUploadTicket_Tests<BlobManagementMongoDbTestModule>
{
}
