using Dignite.Drive.Samples;
using Xunit;

namespace Dignite.Drive.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBSampleDomain_Tests : SampleManager_Tests<DriveMongoDbTestModule>
{

}
