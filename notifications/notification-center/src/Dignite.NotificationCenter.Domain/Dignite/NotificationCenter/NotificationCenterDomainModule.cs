using Dignite.Abp.Notifications;
using Volo.Abp.Domain;
using Volo.Abp.Gdpr;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter;

[DependsOn(
    typeof(NotificationCenterDomainSharedModule),
    typeof(AbpNotificationsAbstractionsModule),
    typeof(AbpDddDomainModule),
    typeof(AbpGdprAbstractionsModule)
    )]
public class NotificationCenterDomainModule : AbpModule
{
}
