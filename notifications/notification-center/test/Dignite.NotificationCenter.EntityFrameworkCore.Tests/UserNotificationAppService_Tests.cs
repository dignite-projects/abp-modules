namespace Dignite.NotificationCenter;

/// <summary>Runs the shared <see cref="IUserNotificationAppService"/> scenarios against EF Core (in-memory Sqlite).</summary>
public class UserNotificationAppService_Tests : UserNotificationAppService_Tests<NotificationCenterEntityFrameworkCoreTestModule>
{
}
