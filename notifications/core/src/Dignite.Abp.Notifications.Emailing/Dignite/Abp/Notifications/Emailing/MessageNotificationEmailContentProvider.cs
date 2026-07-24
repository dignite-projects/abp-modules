using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications.Emailing;

/// <summary>Built-in fallback: emails a <see cref="MessageNotificationData"/> payload's pre-formatted text.</summary>
public class MessageNotificationEmailContentProvider
    : NotificationEmailContentProvider<MessageNotificationData>, ITransientDependency
{
    public override int Order => NotificationEmailProviderOrders.BuiltInFallback;

    protected override Task<NotificationEmail?> BuildOrNullAsync(
        NotificationEmailBuildContext context,
        MessageNotificationData data,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<NotificationEmail?>(
            new NotificationEmail(context.Notification.NotificationName, data.Message));
    }
}
