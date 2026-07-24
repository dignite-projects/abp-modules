using Volo.Abp.AspNetCore.SignalR;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.SignalR;

[DependsOn(
    typeof(AbpNotificationsAbstractionsModule),
    typeof(AbpAspNetCoreSignalRModule)
    )]
public class AbpNotificationsSignalRModule : AbpModule
{
}
