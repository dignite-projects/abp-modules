using Dignite.Drive.MongoDB;
using Dignite.Drive.Samples;
using Xunit;

namespace Dignite.Drive.MongoDb.Applications;

[Collection(MongoTestCollection.Name)]
public class MongoDBSampleAppService_Tests : SampleAppService_Tests<DriveMongoDbTestModule>
{

}
