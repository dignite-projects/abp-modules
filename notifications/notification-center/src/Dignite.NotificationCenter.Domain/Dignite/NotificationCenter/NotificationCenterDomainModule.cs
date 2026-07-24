using Dignite.Abp.Notifications;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter;

[DependsOn(
    typeof(NotificationCenterDomainSharedModule),
    typeof(AbpNotificationsModule),
    typeof(AbpDddDomainModule)
    )]
public class NotificationCenterDomainModule : AbpModule
{
}
