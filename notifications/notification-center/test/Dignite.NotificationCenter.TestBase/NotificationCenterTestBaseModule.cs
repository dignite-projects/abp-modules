using Dignite.Abp.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter;

/// <summary>
/// Shared test infrastructure for the NotificationCenter test suite. Holds everything that is
/// independent of the persistence provider, so both the EF Core and MongoDB provider test projects
/// depend on it and run the exact same store/app-service assertions (see the abstract
/// <c>*_Tests&lt;TStartupModule&gt;</c> classes in this project).
/// </summary>
[DependsOn(
    typeof(NotificationCenterApplicationModule),
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule)
    )]
public class NotificationCenterTestBaseModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysAllowAuthorization();

        // Keep background workers off so the EF outbox test can observe the stored outbox record
        // deterministically (the sender never drains it); harmless for the other providers.
        Configure<AbpBackgroundWorkerOptions>(options => options.IsEnabled = false);

        // Register the custom test payload so the shared serializer can resolve it by discriminator.
        Configure<NotificationDataOptions>(options =>
        {
            options.Add<OrderShippedNotificationData>();
        });
    }
}
