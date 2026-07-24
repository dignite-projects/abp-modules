using Volo.Abp.Emailing;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.Emailing;

[DependsOn(
    typeof(AbpNotificationsAbstractionsModule),
    typeof(AbpEmailingModule)
    )]
public class AbpNotificationsEmailingModule : AbpModule
{
}
