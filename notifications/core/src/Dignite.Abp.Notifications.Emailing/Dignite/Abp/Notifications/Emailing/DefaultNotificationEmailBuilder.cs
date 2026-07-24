using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications.Emailing;

public class DefaultNotificationEmailBuilder : INotificationEmailBuilder, ITransientDependency
{
    protected IReadOnlyList<INotificationEmailContentProvider> ContentProviders { get; }

    public DefaultNotificationEmailBuilder(IEnumerable<INotificationEmailContentProvider> contentProviders)
    {
        ContentProviders = contentProviders
            .OrderBy(provider => provider.Order)
            .ThenBy(provider => provider.GetType().FullName, StringComparer.Ordinal)
            .ToList();
    }

    public virtual async Task<NotificationEmail?> BuildAsync(
        NotificationEmailBuildContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var provider in ContentProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var email = await provider.BuildOrNullAsync(context, cancellationToken);
            if (email != null)
            {
                return email;
            }
        }

        return null;
    }
}
