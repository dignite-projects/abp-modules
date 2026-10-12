using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.Push;

[DependsOn(typeof(AbpNotificationsAbstractionsModule))]
public class AbpNotificationsPushModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<NotificationNotifierOptions>(options =>
        {
            options.Notifiers.Add<PushNotifier>(PushNotifier.ChannelName);
        });
    }
}
