using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter;

[DependsOn(
    typeof(NotificationCenterApplicationContractsModule),
    typeof(NotificationCenterDomainModule),
    typeof(AbpDddApplicationModule)
    )]
public class NotificationCenterApplicationModule : AbpModule
{
}
