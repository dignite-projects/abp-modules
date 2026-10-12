using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The notification definition store, after ABP's <c>AbpPermissionManagementDomainModule</c> (the framework feature's
/// persistence module, as <c>Volo.Abp.BackgroundJobs.Domain</c> is for background jobs): at startup, in the background,
/// it saves this process's static definitions (<see cref="StaticNotificationDefinitionSaver"/>) and, when
/// <see cref="NotificationDefinitionStoreOptions.IsDynamicNotificationStoreEnabled"/> is on, loads everything every
/// process saved (<see cref="DynamicNotificationDefinitionStore"/>, which replaces the empty
/// <see cref="NullDynamicNotificationDefinitionStore"/> of Abstractions). In a data migration environment it does
/// neither.
/// </summary>
/// <remarks>
/// Install it in every publisher (to save) and in the notification service (to save and read). The tables come from
/// <c>Dignite.Abp.Notifications.EntityFrameworkCore</c>, the collections from <c>Dignite.Abp.Notifications.MongoDB</c>.
/// A process that reads the store also needs ABP's own dynamic permission and feature stores on
/// (<c>IsDynamicPermissionStoreEnabled</c>, <c>IsDynamicFeatureStoreEnabled</c>) for the requirements other services
/// define; like ABP, this module does not check another module's switches — the README says so.
/// </remarks>
[DependsOn(
    typeof(AbpNotificationsAbstractionsModule),
    typeof(AbpNotificationsDomainSharedModule),
    typeof(AbpDddDomainModule),
    typeof(AbpCachingModule),
    typeof(AbpDistributedLockingAbstractionsModule),
    typeof(AbpLocalizationModule)
    )]
public class AbpNotificationsDomainModule : AbpModule
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        if (context.Services.IsDataMigrationEnvironment())
        {
            Configure<NotificationDefinitionStoreOptions>(options =>
            {
                options.SaveStaticNotificationsToDatabase = false;
                options.IsDynamicNotificationStoreEnabled = false;
            });
        }
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        AsyncHelper.RunSync(() => OnApplicationInitializationAsync(context));
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var rootServiceProvider = context.ServiceProvider.GetRequiredService<IRootServiceProvider>();
        var initializer = rootServiceProvider.GetRequiredService<NotificationDynamicInitializer>();
        await initializer.InitializeAsync(true, _cancellationTokenSource.Token);
    }

    public override Task OnApplicationShutdownAsync(ApplicationShutdownContext context)
    {
        _cancellationTokenSource.Cancel();
        return Task.CompletedTask;
    }
}
