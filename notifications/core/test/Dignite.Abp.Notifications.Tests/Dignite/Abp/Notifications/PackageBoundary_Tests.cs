using System;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Client;
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
/// What each package brings into a process, assembled from ABP modules. A publisher with Abstractions + Client must not
/// consume the distribution job queue or the delivery events; the in-process implementation registers both. Client and
/// the implementation package do not exclude each other: installed together, the local publisher wins in either module
/// order, as ABP's local <c>UserRoleFinder</c> wins over the <c>TryRegister</c>-ed <c>HttpClientUserRoleFinder</c>.
/// </summary>
public class PackageBoundary_Tests
{
    [Fact]
    public async Task An_Abstractions_and_Client_publisher_has_no_distribution_job_and_no_notification_event_handlers()
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
    public async Task An_implementation_host_registers_the_job_under_its_stable_name_and_both_handlers()
    {
        using var application = await StartAsync<ImplementationHostModule>();
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
    [InlineData(typeof(ClientThenImplementationModule))]
    [InlineData(typeof(ImplementationThenClientModule))]
    public async Task Client_and_the_implementation_in_one_process_resolve_the_local_publisher(Type startupModule)
    {
        using var application = await AbpApplicationFactory.CreateAsync(startupModule, options => options.UseAutofac());
        await application.InitializeAsync();
        var services = application.ServiceProvider;

        services.GetRequiredService<INotificationPublisher>().ShouldBeOfType<DefaultNotificationPublisher>();
        services.GetServices<INotificationPublisher>()
            .ShouldNotContain(publisher => publisher is NullNotificationPublisher);

        // The process distributes itself, so it hosts the job and the handlers like any implementation host.
        services.GetRequiredService<IOptions<AbpBackgroundJobOptions>>().Value
            .GetJob(typeof(NotificationDistributionJobArgs)).JobType.ShouldBe(typeof(NotificationDistributionJob));
        services.GetServices<IDistributedEventHandler<NotificationPublishRequestedEto>>()
            .ShouldContain(handler => handler is NotificationPublishRequestedHandler);

        await application.ShutdownAsync();
    }

    [Fact]
    public async Task Client_alone_wins_over_the_null_publisher()
    {
        using var application = await StartAsync<ClientOnlyModule>();

        application.ServiceProvider.GetServices<INotificationPublisher>()
            .ShouldHaveSingleItem()
            .ShouldBeOfType<RemoteNotificationPublisher>();

        await application.ShutdownAsync();
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

    // A publisher service: background jobs are installed (they would carry the distribution job if the contracts did).
    [DependsOn(
        typeof(AbpNotificationsClientModule),
        typeof(AbpBackgroundJobsAbstractionsModule),
        typeof(AbpAutofacModule))]
    public class RemotePublisherModule : IsolatedTestModule
    {
    }

    [DependsOn(
        typeof(AbpNotificationsModule),
        typeof(AbpAutofacModule))]
    public class ImplementationHostModule : IsolatedTestModule
    {
    }

    [DependsOn(
        typeof(AbpNotificationsClientModule),
        typeof(AbpAutofacModule))]
    public class ClientOnlyModule : IsolatedTestModule
    {
    }

    // Module order follows DependsOn: Client's publisher is registered before the local one here, after it below.
    [DependsOn(
        typeof(AbpNotificationsClientModule),
        typeof(AbpNotificationsModule),
        typeof(AbpAutofacModule))]
    public class ClientThenImplementationModule : IsolatedTestModule
    {
    }

    [DependsOn(
        typeof(AbpNotificationsModule),
        typeof(AbpNotificationsClientModule),
        typeof(AbpAutofacModule))]
    public class ImplementationThenClientModule : IsolatedTestModule
    {
    }
}
