using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The in-process distribution pipeline: the local <see cref="INotificationPublisher"/>, the distributor, the
/// distribution background job, the delivery event handler that calls the channel notifiers, the handler that
/// distributes notifications published in other processes (<see cref="NotificationPublishRequestedEto"/>), and the
/// no-op <see cref="NullNotificationStore"/> of stateless mode. Install it in the process that hosts the inbox and the
/// channels — a monolith, or a dedicated notification service. A publisher whose inbox lives in another process
/// installs <c>Dignite.Abp.Notifications.Remote</c> instead; the two cannot share a process.
/// </summary>
/// <remarks>
/// The contracts these types implement (<see cref="INotificationPublisher"/>, <see cref="INotificationDistributor"/>,
/// <see cref="INotificationStore"/>, <see cref="INotificationPermissionChecker"/>) stay in Core, so a business module
/// and the packages that replace an implementation (the Notification Center's store, the Identity permission checker)
/// never depend on this package.
/// </remarks>
[DependsOn(
    typeof(AbpNotificationsModule),
    typeof(AbpBackgroundJobsAbstractionsModule),
    typeof(AbpEventBusModule),
    typeof(AbpGuidsModule),
    typeof(AbpTimingModule),
    typeof(AbpUnitOfWorkModule)
    )]
public class AbpNotificationsDistributionModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services
            .AddOptions<NotificationDistributionOptions>()
            .Validate(options =>
            {
                options.Validate();
                return true;
            })
            .ValidateOnStart();

        // The two checks of the routing table that only make sense where channels are delivered: a channel no notifier
        // in this process hosts, and a stateless host (no inbox) with a notification that has no channel at all.
        context.Services.AddHostedService<NotificationDistributionStartupService>();

        Configure<AbpDistributedEventBusOptions>(options =>
        {
            options.Handlers.Add<NotificationDeliveryRequestedHandler>();
            options.Handlers.Add<NotificationPublishRequestedHandler>();
        });
    }
}
