using Dignite.Abp.Notifications;
using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter;

/// <remarks>
/// Depends on <see cref="AbpNotificationsDistributionModule"/>: the process that serves the inbox is the process that
/// distributes (subscription changes go through its <c>NotificationSubscriptionManager</c>), so a host with the
/// Notification Center's application layer cannot also be a remote publisher.
/// </remarks>
[DependsOn(
    typeof(NotificationCenterApplicationContractsModule),
    typeof(NotificationCenterDomainModule),
    typeof(AbpNotificationsDistributionModule),
    typeof(AbpDddApplicationModule)
    )]
public class NotificationCenterApplicationModule : AbpModule
{
}
