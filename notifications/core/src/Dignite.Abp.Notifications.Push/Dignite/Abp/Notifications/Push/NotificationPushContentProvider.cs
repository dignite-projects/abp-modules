using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications.Push;

/// <summary>
/// Base class for a content provider that handles one <see cref="NotificationData"/> type. Narrows the payload once,
/// so an implementer cannot forget to and accidentally claim every notification in the system.
/// </summary>
/// <remarks>
/// The chain contract stays non-generic (<see cref="INotificationPushContentProvider"/>) for the same reason as the
/// email chain: the builder orders every provider together, and the <c>is TData</c> test keeps subtype matching that a
/// DI lookup closed over <c>Data.GetType()</c> would lose.
/// </remarks>
/// <typeparam name="TData">The payload type this provider builds content for, including its subclasses.</typeparam>
public abstract class NotificationPushContentProvider<TData> : INotificationPushContentProvider
    where TData : NotificationData
{
    /// <summary>Defaults to <see cref="NotificationPushProviderOrders.Default"/>, ahead of the built-ins.</summary>
    public virtual int Order => NotificationPushProviderOrders.Default;

    /// <summary>
    /// Deliberately not virtual: overriding it would reintroduce the chance to drop the payload guard.
    /// </summary>
    public Task<NotificationPushContent?> BuildOrNullAsync(
        NotificationPushBuildContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return context.Notification.Data is TData data
            ? BuildOrNullAsync(context, data, cancellationToken)
            : Task.FromResult<NotificationPushContent?>(null);
    }

    /// <summary>
    /// Builds the content for a payload already narrowed to <typeparamref name="TData"/>. Return null to pass the
    /// notification to the next provider — for example when this payload is not worth a push.
    /// </summary>
    protected abstract Task<NotificationPushContent?> BuildOrNullAsync(
        NotificationPushBuildContext context,
        TData data,
        CancellationToken cancellationToken);
}
