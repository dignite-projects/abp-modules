using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications.Emailing;

/// <summary>
/// Base class for a content provider that handles one <see cref="NotificationData"/> type. Narrows the payload once,
/// so an implementer cannot forget to and accidentally claim every notification in the system.
/// </summary>
/// <remarks>
/// The chain contract stays non-generic (<see cref="INotificationEmailContentProvider"/>) on purpose:
/// <see cref="DefaultNotificationEmailBuilder"/> injects <c>IEnumerable&lt;INotificationEmailContentProvider&gt;</c>
/// and orders every provider together regardless of payload type. A generic <i>interface</i> would force the builder
/// to resolve <c>INotificationEmailContentProvider&lt;&gt;</c> closed over <c>Data.GetType()</c>, and that lookup is
/// exact — a provider typed on <c>MessageNotificationData</c> would stop handling a host's
/// <c>PromoNotificationData : MessageNotificationData</c>. The <c>is TData</c> test below keeps subtype matching.
/// <para>
/// A provider that genuinely handles two unrelated payload types implements
/// <see cref="INotificationEmailContentProvider"/> directly.
/// </para>
/// </remarks>
/// <typeparam name="TData">The payload type this provider builds content for, including its subclasses.</typeparam>
public abstract class NotificationEmailContentProvider<TData> : INotificationEmailContentProvider
    where TData : NotificationData
{
    /// <summary>Defaults to <see cref="NotificationEmailProviderOrders.Default"/>, ahead of the built-ins.</summary>
    public virtual int Order => NotificationEmailProviderOrders.Default;

    /// <summary>
    /// Deliberately not virtual: overriding it would reintroduce the chance to drop the payload guard.
    /// </summary>
    public Task<NotificationEmail?> BuildOrNullAsync(
        NotificationEmailBuildContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return context.Notification.Data is TData data
            ? BuildOrNullAsync(context, data, cancellationToken)
            : Task.FromResult<NotificationEmail?>(null);
    }

    /// <summary>
    /// Builds the email for a payload already narrowed to <typeparamref name="TData"/>. Return null to pass the
    /// notification to the next provider — for example when this payload carries nothing worth emailing.
    /// </summary>
    protected abstract Task<NotificationEmail?> BuildOrNullAsync(
        NotificationEmailBuildContext context,
        TData data,
        CancellationToken cancellationToken);
}
