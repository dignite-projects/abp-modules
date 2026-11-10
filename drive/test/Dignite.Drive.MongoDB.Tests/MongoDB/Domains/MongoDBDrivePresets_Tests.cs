using Xunit;

namespace Dignite.Drive.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBDrivePresets_Tests : DrivePresets_Tests<DriveMongoDbTestModule>
{
}
