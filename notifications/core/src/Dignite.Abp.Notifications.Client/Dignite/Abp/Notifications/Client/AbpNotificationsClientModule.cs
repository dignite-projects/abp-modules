using Volo.Abp.EventBus;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;

namespace Dignite.Abp.Notifications.Client;

/// <summary>
/// Remote publishing: <see cref="INotificationPublisher"/> becomes <see cref="RemoteNotificationPublisher"/>, which sends
/// one <see cref="NotificationPublishRequestedEto"/> per notification to the process that hosts the inbox and the
/// channels (where <c>Dignite.Abp.Notifications</c> handles it). Install it in a publisher that does not host the inbox;
/// such a process registers no distribution job and handles no delivery events. The package is named after ABP's
/// <c>Volo.Abp.AspNetCore.Mvc.Client</c>, the package of <c>RemotePermissionChecker</c> and its siblings.
/// </summary>
/// <remarks>
/// It does not exclude the in-process implementation. <see cref="RemoteNotificationPublisher"/> registers with
/// <c>TryRegister</c>, as ABP's <c>HttpClientUserRoleFinder</c> does next to the local <c>UserRoleFinder</c>: in a process
/// that also installs <c>Dignite.Abp.Notifications</c> the local <c>DefaultNotificationPublisher</c> wins, whatever the
/// module order, and the process distributes its notifications itself.
/// </remarks>
[DependsOn(
    typeof(AbpNotificationsAbstractionsModule),
    typeof(AbpEventBusModule),
    typeof(AbpGuidsModule),
    typeof(AbpTimingModule)
    )]
public class AbpNotificationsClientModule : AbpModule
{
}
