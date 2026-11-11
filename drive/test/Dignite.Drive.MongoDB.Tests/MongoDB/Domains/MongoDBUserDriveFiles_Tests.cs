using Xunit;

namespace Dignite.Drive.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBUserDriveFiles_Tests : UserDriveFiles_Tests<DriveMongoDbTestModule>
{
}
