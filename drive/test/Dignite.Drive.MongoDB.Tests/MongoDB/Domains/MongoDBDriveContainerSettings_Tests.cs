using Xunit;

namespace Dignite.Drive.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBDriveContainerSettings_Tests : DriveContainerSettings_Tests<DriveMongoDbTestModule>
{
}
