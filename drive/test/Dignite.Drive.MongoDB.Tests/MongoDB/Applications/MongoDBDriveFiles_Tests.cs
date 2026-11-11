using Xunit;

namespace Dignite.Drive.MongoDB.Applications;

[Collection(MongoTestCollection.Name)]
public class MongoDBDriveFiles_Tests : DriveFiles_Tests<DriveMongoDbTestModule>
{
}
