using Dignite.NotificationCenter.MongoDB;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>Runs the shared notification-service scenarios for remotely published notifications against MongoDB.</summary>
[Collection(MongoTestCollection.Name)]
public class NotificationPublishRequested_Tests
    : NotificationPublishRequested_Tests<NotificationCenterMongoDbTestModule>
{
}
