using Volo.Abp.Identity;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter.Push.Identity;

[DependsOn(
    typeof(AbpNotificationCenterPushModule),
    typeof(AbpIdentityDomainModule)
    )]
public class AbpNotificationCenterPushIdentityModule : AbpModule
{
}
