using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Resolves channels from <see cref="NotificationRoutingOptions"/>: the notification's own rule when there is one
/// (an empty rule is explicit inbox-only), otherwise <see cref="NotificationRoutingOptions.Default"/>.
/// </summary>
public class DefaultNotificationChannelResolver : INotificationChannelResolver, ITransientDependency
{
    protected NotificationRoutingOptions Options { get; }

    public DefaultNotificationChannelResolver(IOptions<NotificationRoutingOptions> options)
    {
        Options = options.Value;
    }

    public virtual Task<string[]?> ResolveAsync(
        NotificationDefinition definition,
        NotificationInfo notification,
        CancellationToken cancellationToken = default)
    {
        var channels = Options.Notifications.TryGetValue(definition.Name, out var rule)
            ? rule
            : Options.Default;

        if (channels == null || channels.Length == 0)
        {
            return Task.FromResult<string[]?>(null);
        }

        return Task.FromResult<string[]?>(NotificationRoutingOptions.NormalizeChannels(channels));
    }
}
