using Dignite.Abp.Notifications;
using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter;

[DependsOn(
    typeof(NotificationCenterDomainSharedModule),
    typeof(AbpNotificationsAbstractionsModule),
    typeof(AbpAuthorizationAbstractionsModule),
    typeof(AbpDddApplicationContractsModule)
    )]
public class NotificationCenterApplicationContractsModule : AbpModule
{
}
