using Xunit;

namespace Dignite.Drive.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBFileDescriptorManager_Tests : FileDescriptorManager_Tests<DriveMongoDbTestModule>
{
}
