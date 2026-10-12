using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications.Push;

/// <summary>Walks the content providers in order and takes the first non-null result.</summary>
public class DefaultNotificationPushBuilder : INotificationPushBuilder, ITransientDependency
{
    protected IReadOnlyList<INotificationPushContentProvider> ContentProviders { get; }

    public DefaultNotificationPushBuilder(IEnumerable<INotificationPushContentProvider> contentProviders)
    {
        // Order-then-FullName-Ordinal tiebreak, so which provider wins never depends on DI registration order.
        ContentProviders = contentProviders
            .OrderBy(provider => provider.Order)
            .ThenBy(provider => provider.GetType().FullName, StringComparer.Ordinal)
            .ToList();
    }

    public virtual async Task<NotificationPushContent?> BuildAsync(
        NotificationPushBuildContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var provider in ContentProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await provider.BuildOrNullAsync(context, cancellationToken);
            if (content != null)
            {
                return content;
            }
        }

        return null;
    }
}
