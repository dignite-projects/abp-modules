using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;

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
        var localizer = data.ResourceName != null
            ? StringLocalizerFactory.CreateByResourceNameOrNull(data.ResourceName)
            : null;
        localizer ??= StringLocalizerFactory.CreateDefaultOrNull();

        var body = localizer == null
            ? data.Name
            : data.Arguments != null
                ? localizer[data.Name, data.Arguments.Values.ToArray()].Value
                : localizer[data.Name].Value;

        return Task.FromResult<NotificationPushContent?>(string.IsNullOrWhiteSpace(body)
            ? null
            : new NotificationPushContent(null, body));
    }
}
