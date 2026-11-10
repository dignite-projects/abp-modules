using Xunit;

namespace Dignite.Drive.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBDriveUsageManager_Tests : DriveUsageManager_Tests<DriveMongoDbTestModule>
{
}
