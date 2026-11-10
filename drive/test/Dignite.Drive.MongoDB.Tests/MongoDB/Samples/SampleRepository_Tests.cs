using Dignite.Drive.Samples;
using Xunit;

namespace Dignite.Drive.MongoDB.Samples;

[Collection(MongoTestCollection.Name)]
public class SampleRepository_Tests : SampleRepository_Tests<DriveMongoDbTestModule>
{
    /* Don't write custom repository tests here, instead write to
     * the base class.
     * One exception can be some specific tests related to MongoDB.
     */
}
