using Dignite.Abp.Notifications;
using Dignite.NotificationCenter.Web.Components.NotificationBell;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dignite.NotificationCenter.Web;

/// <summary>
/// Builds the per-item rendering model (payload discriminator + resolved entity link) for the inbox page, using
/// the same <see cref="NotificationCenterWebOptions"/> extension points as the bell.
/// </summary>
public class NotificationItemViewModelFactory : ITransientDependency
{
    protected INotificationDataTypeRegistry NotificationDataTypeRegistry { get; }

    protected NotificationCenterWebOptions WebOptions { get; }

    public NotificationItemViewModelFactory(
        INotificationDataTypeRegistry notificationDataTypeRegistry,
        IOptions<NotificationCenterWebOptions> webOptions)
    {
        NotificationDataTypeRegistry = notificationDataTypeRegistry;
        WebOptions = webOptions.Value;
    }

    public virtual NotificationBellItemViewModel Create(UserNotificationDto notification)
    {
        var discriminator = notification.Data == null
            ? null
            : NotificationDataTypeRegistry.GetDiscriminatorOrNull(notification.Data.GetType());

        return new NotificationBellItemViewModel(notification, discriminator, ResolveEntityUrl(notification));
    }

    protected virtual string? ResolveEntityUrl(UserNotificationDto notification)
    {
        if (notification.EntityTypeName == null || notification.EntityId == null)
        {
            return null;
        }

        return WebOptions.EntityLinkResolvers.TryGetValue(notification.EntityTypeName, out var resolve)
            ? resolve(notification.EntityId)
            : null;
    }
}
