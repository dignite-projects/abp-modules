using Dignite.NotificationCenter.MongoDB;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>Runs the shared <see cref="IUserNotificationAppService"/> scenarios against the MongoDB provider.</summary>
[Collection(MongoTestCollection.Name)]
public class UserNotificationAppService_Tests : UserNotificationAppService_Tests<NotificationCenterMongoDbTestModule>
{
}
