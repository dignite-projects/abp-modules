using Dignite.NotificationCenter.MongoDB;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>Runs the shared notification-store scenarios against the MongoDB provider.</summary>
[Collection(MongoTestCollection.Name)]
public class NotificationStore_Tests : NotificationStore_Tests<NotificationCenterMongoDbTestModule>
{
}
