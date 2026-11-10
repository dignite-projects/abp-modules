using Xunit;

namespace Dignite.Drive.MongoDB.Applications;

[Collection(MongoTestCollection.Name)]
public class MongoDBDriveBlobProvider_Tests : DriveBlobProvider_Tests<DriveMongoDbTestModule>
{
}
