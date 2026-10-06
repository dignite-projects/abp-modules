using Dignite.Abp.Notifications.Push;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter.Push;

[DependsOn(
    typeof(NotificationCenterDomainModule),
    typeof(AbpNotificationsPushModule)
    )]
public class NotificationCenterPushModule : AbpModule
{
}
