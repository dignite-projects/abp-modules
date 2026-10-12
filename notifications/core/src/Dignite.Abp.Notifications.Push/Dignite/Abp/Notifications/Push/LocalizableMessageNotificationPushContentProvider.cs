using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications.Push;

/// <summary>
/// Built-in fallback: localizes a <see cref="LocalizableMessageNotificationData"/> payload at send time, in the
/// device's culture rather than the publisher's.
/// </summary>
public class LocalizableMessageNotificationPushContentProvider
    : NotificationPushContentProvider<LocalizableMessageNotificationData>, ITransientDependency
{
    protected IStringLocalizerFactory StringLocalizerFactory { get; }

    public override int Order => NotificationPushProviderOrders.BuiltInFallback;

    public LocalizableMessageNotificationPushContentProvider(IStringLocalizerFactory stringLocalizerFactory)
    {
        StringLocalizerFactory = stringLocalizerFactory;
    }

    protected override Task<NotificationPushContent?> BuildOrNullAsync(
        NotificationPushBuildContext context,
        LocalizableMessageNotificationData data,
        CancellationToken cancellationToken)
    {
        var body = data.Localize(StringLocalizerFactory);

        return Task.FromResult<NotificationPushContent?>(string.IsNullOrWhiteSpace(body)
            ? null
            : new NotificationPushContent(null, body));
    }
}
