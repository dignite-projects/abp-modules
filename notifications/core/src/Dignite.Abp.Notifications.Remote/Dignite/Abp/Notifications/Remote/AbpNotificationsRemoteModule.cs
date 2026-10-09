using System.Linq;
using Volo.Abp;
using Volo.Abp.EventBus;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;

namespace Dignite.Abp.Notifications.Remote;

/// <summary>
/// Remote publishing: <see cref="INotificationPublisher"/> becomes <see cref="RemoteNotificationPublisher"/>, which sends
/// one <see cref="NotificationPublishRequestedEto"/> per notification to the process that hosts the inbox and the
/// channels (where <c>Dignite.Abp.Notifications.Distribution</c> handles it). Install it in a publisher that does not
/// host the inbox; such a process registers no distribution job and handles no delivery events.
/// </summary>
/// <remarks>
/// Remote and Distribution cannot share a process: the start fails if anything here registers an
/// <see cref="INotificationDistributor"/> (Distribution does, and so does the Notification Center's application layer,
/// which depends on it).
/// </remarks>
[DependsOn(
    typeof(AbpNotificationsAbstractionsModule),
    typeof(AbpEventBusModule),
    typeof(AbpGuidsModule),
    typeof(AbpTimingModule)
    )]
public class AbpNotificationsRemoteModule : AbpModule
{
    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        // Checked against the contract, not a module type, so neither package needs to know the other. Every module has
        // registered its services by now, whatever the order.
        if (context.Services.Any(descriptor => descriptor.ServiceType == typeof(INotificationDistributor)))
        {
            throw new AbpException(
                "Dignite.Abp.Notifications.Remote and Dignite.Abp.Notifications.Distribution are both installed in this " +
                "process (an INotificationDistributor is registered). Install one of them: Distribution in the process that " +
                "hosts the inbox and the channels, Remote in a publisher whose notifications are distributed by another " +
                "process. The Notification Center's application layer brings Distribution with it.");
        }
    }
}
