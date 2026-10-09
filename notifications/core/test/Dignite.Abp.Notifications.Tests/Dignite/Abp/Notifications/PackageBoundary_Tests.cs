using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Remote;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// What each package brings into a process, assembled from ABP modules. A publisher with Core + Remote must not consume
/// the distribution job queue or the delivery events (both used to arrive with Core); Remote and Distribution must
/// not share a process.
/// </summary>
public class PackageBoundary_Tests
{
    [Fact]
    public async Task A_Core_and_Remote_publisher_has_no_distribution_job_and_no_notification_event_handlers()
    {
        using var application = await StartAsync<RemotePublisherModule>();
        var services = application.ServiceProvider;

        // ABP's RabbitMQ job queue manager starts one consumer per entry of AbpBackgroundJobOptions.GetJobs().
        services.GetRequiredService<IOptions<AbpBackgroundJobOptions>>().Value.GetJobs()
            .ShouldNotContain(job =>
                job.ArgsType == typeof(NotificationDistributionJobArgs) ||
                job.JobName == "Dignite.Abp.Notifications.Distribute");

        // The distributed event bus subscribes AbpDistributedEventBusOptions.Handlers.
        var handlers = services.GetRequiredService<IOptions<AbpDistributedEventBusOptions>>().Value.Handlers;
        handlers.ShouldNotContain(handler =>
            typeof(IDistributedEventHandler<NotificationDeliveryRequestedEto>).IsAssignableFrom(handler) ||
            typeof(IDistributedEventHandler<NotificationPublishRequestedEto>).IsAssignableFrom(handler));
        services.GetServices<IDistributedEventHandler<NotificationDeliveryRequestedEto>>().ShouldBeEmpty();
        services.GetServices<IDistributedEventHandler<NotificationPublishRequestedEto>>().ShouldBeEmpty();

        services.GetRequiredService<INotificationPublisher>().ShouldBeOfType<RemoteNotificationPublisher>();
        services.GetService<INotificationDistributor>().ShouldBeNull();
        services.GetRequiredService<INotificationStore>().ShouldBeOfType<NullNotificationStore>();

        await application.ShutdownAsync();
    }

    [Fact]
    public async Task A_Distribution_host_registers_the_job_under_its_stable_name_and_both_handlers()
    {
        using var application = await StartAsync<DistributionHostModule>();
        var services = application.ServiceProvider;

        var job = services.GetRequiredService<IOptions<AbpBackgroundJobOptions>>().Value
            .GetJob(typeof(NotificationDistributionJobArgs));
        job.JobName.ShouldBe("Dignite.Abp.Notifications.Distribute");
        job.JobType.ShouldBe(typeof(NotificationDistributionJob));

        services.GetServices<IDistributedEventHandler<NotificationDeliveryRequestedEto>>()
            .ShouldContain(handler => handler is NotificationDeliveryRequestedHandler);
        services.GetServices<IDistributedEventHandler<NotificationPublishRequestedEto>>()
            .ShouldContain(handler => handler is NotificationPublishRequestedHandler);
        services.GetRequiredService<INotificationPublisher>().ShouldBeOfType<DefaultNotificationPublisher>();

        await application.ShutdownAsync();
    }

    [Fact]
    public void The_job_name_does_not_depend_on_the_args_type_name()
    {
        BackgroundJobNameAttribute.GetName<NotificationDistributionJobArgs>()
            .ShouldBe("Dignite.Abp.Notifications.Distribute");
    }

    [Theory]
    [InlineData(typeof(RemoteThenDistributionModule))]
    [InlineData(typeof(DistributionThenRemoteModule))]
    public async Task Remote_and_Distribution_in_one_process_fail_the_start(Type startupModule)
    {
        var exception = await Should.ThrowAsync<Exception>(() => AbpApplicationFactory.CreateAsync(
            startupModule,
            options => options.UseAutofac()));

        var message = exception.ToString();
        message.ShouldContain("Dignite.Abp.Notifications.Remote and Dignite.Abp.Notifications.Distribution are both installed");
    }

    private static async Task<IAbpApplicationWithInternalServiceProvider> StartAsync<TModule>()
        where TModule : IAbpModule
    {
        var application = await AbpApplicationFactory.CreateAsync<TModule>(options => options.UseAutofac());
        await application.InitializeAsync();
        return application;
    }

    /// <summary>The test assembly is not conventionally registered, so only the packages under test contribute.</summary>
    public abstract class IsolatedTestModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            SkipAutoServiceRegistration = true;
        }
    }

    // A publisher service: background jobs are installed (they would carry the distribution job if Core still did).
    [DependsOn(
        typeof(AbpNotificationsRemoteModule),
        typeof(AbpBackgroundJobsAbstractionsModule),
        typeof(AbpAutofacModule))]
    public class RemotePublisherModule : IsolatedTestModule
    {
    }

    [DependsOn(
        typeof(AbpNotificationsModule),
        typeof(AbpAutofacModule))]
    public class DistributionHostModule : IsolatedTestModule
    {
    }

    [DependsOn(
        typeof(AbpNotificationsRemoteModule),
        typeof(AbpNotificationsModule),
        typeof(AbpAutofacModule))]
    public class RemoteThenDistributionModule : IsolatedTestModule
    {
    }

    [DependsOn(
        typeof(AbpNotificationsModule),
        typeof(AbpNotificationsRemoteModule),
        typeof(AbpAutofacModule))]
    public class DistributionThenRemoteModule : IsolatedTestModule
    {
    }
}
