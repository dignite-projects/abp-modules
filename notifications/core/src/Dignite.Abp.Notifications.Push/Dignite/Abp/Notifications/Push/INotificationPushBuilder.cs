using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications.Push;

/// <summary>Turns a notification into push content. Business modules add content providers rather than replace this.</summary>
public interface INotificationPushBuilder
{
    Task<NotificationPushContent?> BuildAsync(
        NotificationPushBuildContext context,
        CancellationToken cancellationToken = default);
}
