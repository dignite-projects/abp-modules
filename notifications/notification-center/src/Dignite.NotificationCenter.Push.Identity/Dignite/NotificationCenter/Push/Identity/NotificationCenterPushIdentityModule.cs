using Volo.Abp.Identity;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter.Push.Identity;

[DependsOn(
    typeof(NotificationCenterPushModule),
    typeof(AbpIdentityDomainModule)
    )]
public class NotificationCenterPushIdentityModule : AbpModule
{
}
