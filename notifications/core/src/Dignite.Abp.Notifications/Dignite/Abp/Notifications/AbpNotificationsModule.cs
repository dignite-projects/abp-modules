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
/// The default, in-process implementation of the contracts in <see cref="AbpNotificationsAbstractionsModule"/>, as
/// <c>AbpBackgroundJobsModule</c> is for <c>AbpBackgroundJobsAbstractionsModule</c>: the local
/// <see cref="INotificationPublisher"/>, the distributor, the distribution background job, the delivery event handler
/// that calls the channel notifiers, and the handler that distributes notifications published in other processes
/// (<see cref="NotificationPublishRequestedEto"/>). Install it in the process that hosts the inbox and the channels — a
/// monolith, or a dedicated notification service.
/// </summary>
/// <remarks>
/// A business module depends on <see cref="AbpNotificationsAbstractionsModule"/>, never on this module: depending on it
/// would bring the distributor, the job and the event handlers into every process that hosts the business module. A
/// publisher whose inbox lives in another process installs <c>Dignite.Abp.Notifications.Client</c> instead. The two do not
/// exclude each other: installed together, this package's <see cref="DefaultNotificationPublisher"/> wins in either module
/// order and the process distributes its notifications itself.
/// </remarks>
[DependsOn(
    typeof(AbpNotificationsAbstractionsModule),
    typeof(AbpBackgroundJobsAbstractionsModule),
    typeof(AbpEventBusModule),
    typeof(AbpGuidsModule),
    typeof(AbpTimingModule),
    typeof(AbpUnitOfWorkModule)
    )]
public class AbpNotificationsModule : AbpModule
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
