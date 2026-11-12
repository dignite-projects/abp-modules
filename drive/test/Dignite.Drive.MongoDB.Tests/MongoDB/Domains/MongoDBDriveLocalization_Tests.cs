using Xunit;

namespace Dignite.Drive.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBDriveLocalization_Tests : DriveLocalization_Tests<DriveMongoDbTestModule>
{
}
