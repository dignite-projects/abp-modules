using Dignite.NotificationCenter.MongoDB;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>Runs the shared push device registry scenarios against the MongoDB provider.</summary>
[Collection(MongoTestCollection.Name)]
public class PushDevice_Tests : PushDevice_Tests<NotificationCenterMongoDbTestModule>
{
}
