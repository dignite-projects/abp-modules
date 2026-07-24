using Dignite.Abp.Notifications.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.Emailing.Identity;

[DependsOn(
    typeof(AbpNotificationsEmailingModule),
    typeof(AbpIdentityDomainModule)
    )]
public class AbpNotificationsEmailingIdentityModule : AbpModule
{
}

