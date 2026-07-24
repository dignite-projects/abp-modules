using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Volo.Abp;
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
        var localizer = data.ResourceName != null
            ? StringLocalizerFactory.CreateByResourceNameOrNull(data.ResourceName)
            : null;
        localizer ??= StringLocalizerFactory.CreateDefaultOrNull();

        var body = localizer == null
            ? data.Name
            : data.Arguments != null
                ? localizer[data.Name, data.Arguments.Values.ToArray()].Value
                : localizer[data.Name].Value;

        return Task.FromResult<NotificationEmail?>(
            new NotificationEmail(context.Notification.NotificationName, body));
    }
}
