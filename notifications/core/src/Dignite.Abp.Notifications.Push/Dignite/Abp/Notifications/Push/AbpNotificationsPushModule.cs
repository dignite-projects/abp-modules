using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.Push;

[DependsOn(typeof(AbpNotificationsAbstractionsModule))]
public class AbpNotificationsPushModule : AbpModule
{
}
