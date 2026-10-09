using Dignite.Abp.Notifications;
using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter;

/// <remarks>
/// Depends on <see cref="AbpNotificationsModule"/>, the in-process implementation: the process that serves the inbox
/// is the process that distributes (subscription changes go through its <c>NotificationSubscriptionManager</c>), so a
/// host with the Notification Center's application layer publishes locally even if it also installs
/// <c>Dignite.Abp.Notifications.Client</c>.
/// </remarks>
[DependsOn(
    typeof(NotificationCenterApplicationContractsModule),
    typeof(NotificationCenterDomainModule),
    typeof(AbpNotificationsModule),
    typeof(AbpDddApplicationModule)
    )]
public class NotificationCenterApplicationModule : AbpModule
{
}
