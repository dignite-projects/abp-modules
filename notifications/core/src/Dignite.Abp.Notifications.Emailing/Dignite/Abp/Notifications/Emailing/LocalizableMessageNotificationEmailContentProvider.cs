using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications.Emailing;

/// <summary>
/// Built-in fallback: localizes a <see cref="LocalizableMessageNotificationData"/> payload at send time, in the
/// reader's culture rather than the publisher's.
/// </summary>
public class LocalizableMessageNotificationEmailContentProvider
    : NotificationEmailContentProvider<LocalizableMessageNotificationData>, ITransientDependency
{
    protected IStringLocalizerFactory StringLocalizerFactory { get; }

    public override int Order => NotificationEmailProviderOrders.BuiltInFallback;

    public LocalizableMessageNotificationEmailContentProvider(IStringLocalizerFactory stringLocalizerFactory)
    {
        StringLocalizerFactory = stringLocalizerFactory;
    }

    protected override Task<NotificationEmail?> BuildOrNullAsync(
        NotificationEmailBuildContext context,
        LocalizableMessageNotificationData data,
        CancellationToken cancellationToken)
    {
        var body = data.Localize(StringLocalizerFactory);

        return Task.FromResult<NotificationEmail?>(
            new NotificationEmail(context.Notification.NotificationName, body));
    }
}
