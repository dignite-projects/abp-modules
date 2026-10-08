using Dignite.NotificationCenter.MongoDB;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>Runs the shared GDPR user data deletion scenarios against the MongoDB provider.</summary>
[Collection(MongoTestCollection.Name)]
public class GdprUserDataDeletion_Tests : GdprUserDataDeletion_Tests<NotificationCenterMongoDbTestModule>
{
}
