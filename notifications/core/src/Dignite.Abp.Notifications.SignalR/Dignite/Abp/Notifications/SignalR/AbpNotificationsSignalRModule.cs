using Volo.Abp.AspNetCore.SignalR;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.SignalR;

[DependsOn(
    typeof(AbpNotificationsAbstractionsModule),
    typeof(AbpAspNetCoreSignalRModule)
    )]
public class AbpNotificationsSignalRModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<NotificationNotifierOptions>(options =>
        {
            options.Notifiers.Add<SignalRNotifier>(SignalRNotifier.ChannelName);
        });
    }
}
