using Xunit;

namespace Dignite.Drive.MongoDB.Applications;

[Collection(MongoTestCollection.Name)]
public class MongoDBDriveFileUploadTicket_Tests : DriveFileUploadTicket_Tests<DriveMongoDbTestModule>
{
}
