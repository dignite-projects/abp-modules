using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;
using Volo.Abp.EventBus.Distributed;
using Dignite.Abp.Notifications.SignalR;

namespace Dignite.Abp.Notifications;

[DependsOn(
    typeof(AbpNotificationsDistributionModule),
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule)
    )]
public class DigniteAbpNotificationsTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpBackgroundWorkerOptions>(options => options.IsEnabled = false);

        // Collector for notification delivery ETOs delivered through the (local) distributed event bus.
        context.Services.AddSingleton<ReceivedNotificationDeliveries>();
        Configure<AbpDistributedEventBusOptions>(options =>
        {
            options.Handlers.Add<TestNotificationDeliveryHandler>();
        });

        // No real background job infrastructure in tests — swap in a fake we can inspect.
        context.Services.AddSingleton<FakeBackgroundJobManager>();
        context.Services.Replace(
            ServiceDescriptor.Singleton<IBackgroundJobManager>(
                sp => sp.GetRequiredService<FakeBackgroundJobManager>()));

        // Every test notification is delivered on the SignalR channel.
        Configure<NotificationRoutingOptions>(options =>
        {
            options.ForNotifications(
                [
                    TestNotificationDefinitionProvider.Plain,
                    TestNotificationDefinitionProvider.FeatureGated,
                    TestNotificationDefinitionProvider.DisabledFeatureGated,
                    TestNotificationDefinitionProvider.PermissionGranted,
                    TestNotificationDefinitionProvider.PermissionDenied
                ],
                SignalRNotifier.ChannelName);
        });

        // Permission checks: swap the always-grant default for a fake that denies one specific permission.
        context.Services.Replace(
            ServiceDescriptor.Singleton<INotificationPermissionChecker, TestNotificationPermissionChecker>());
    }
}
