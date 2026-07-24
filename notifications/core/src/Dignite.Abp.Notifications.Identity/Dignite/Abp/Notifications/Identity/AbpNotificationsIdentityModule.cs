using Volo.Abp.Identity;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.Identity;

[DependsOn(
    typeof(AbpNotificationsModule),
    typeof(AbpIdentityDomainModule)
    )]
public class AbpNotificationsIdentityModule : AbpModule
{
}
