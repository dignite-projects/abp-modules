using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications.Push;

/// <summary>
/// Built-in fallback: pushes a <see cref="MessageNotificationData"/> payload's pre-formatted text, with no title so
/// the operating system shows the app name.
/// </summary>
public class MessageNotificationPushContentProvider
    : NotificationPushContentProvider<MessageNotificationData>, ITransientDependency
{
    public override int Order => NotificationPushProviderOrders.BuiltInFallback;

    protected override Task<NotificationPushContent?> BuildOrNullAsync(
        NotificationPushBuildContext context,
        MessageNotificationData data,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<NotificationPushContent?>(string.IsNullOrWhiteSpace(data.Message)
            ? null
            : new NotificationPushContent(null, data.Message));
    }
}
