using Volo.Abp.Emailing;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.Emailing;

[DependsOn(
    typeof(AbpNotificationsAbstractionsModule),
    typeof(AbpEmailingModule)
    )]
public class AbpNotificationsEmailingModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<NotificationNotifierOptions>(options =>
        {
            options.Notifiers.Add<EmailNotifier>(EmailNotifier.ChannelName);
        });
    }
}
