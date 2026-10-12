using Dignite.Abp.Notifications.Push;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter.Push;

[DependsOn(
    typeof(AbpNotificationCenterDomainModule),
    typeof(AbpNotificationsPushModule)
    )]
public class AbpNotificationCenterPushModule : AbpModule
{
}
