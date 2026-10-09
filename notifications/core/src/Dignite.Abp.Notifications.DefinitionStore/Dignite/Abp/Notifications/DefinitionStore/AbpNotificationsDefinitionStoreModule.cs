using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Threading;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>
/// The notification definition store, after ABP's <c>AbpPermissionManagementDomainModule</c>: at startup, in the
/// background, it saves this process's static definitions (<see cref="StaticNotificationDefinitionSaver"/>) and, when
/// <see cref="NotificationDefinitionStoreOptions.IsDynamicNotificationStoreEnabled"/> is on, loads everything every
/// process saved (<see cref="DynamicNotificationDefinitionStore"/>, which replaces Core's empty
/// <see cref="NullDynamicNotificationDefinitionStore"/>). In a data migration environment it does neither.
/// </summary>
/// <remarks>
/// Install it in every publisher (to save) and in the notification service (to save and read). The tables come from
/// <c>Dignite.Abp.Notifications.DefinitionStore.EntityFrameworkCore</c>.
/// </remarks>
[DependsOn(
    typeof(AbpNotificationsModule),
    typeof(AbpDddDomainModule),
    typeof(AbpCachingModule),
    typeof(AbpDistributedLockingAbstractionsModule),
    typeof(AbpLocalizationModule)
    )]
public class AbpNotificationsDefinitionStoreModule : AbpModule
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
        WarnAboutStaticRequirementStores(context);

        var rootServiceProvider = context.ServiceProvider.GetRequiredService<IRootServiceProvider>();
        var initializer = rootServiceProvider.GetRequiredService<NotificationDynamicInitializer>();
        await initializer.InitializeAsync(true, _cancellationTokenSource.Token);
    }

    public override Task OnApplicationShutdownAsync(ApplicationShutdownContext context)
    {
        _cancellationTokenSource.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>
    /// A process that reads other processes' definitions also applies their permission and feature requirements. ABP
    /// knows a permission or feature another service defines only through its own dynamic stores; without them the
    /// check finds no such name and answers false, so every recipient of such a notification is filtered out without
    /// an error.
    /// </summary>
    private static void WarnAboutStaticRequirementStores(ApplicationInitializationContext context)
    {
        var options = context.ServiceProvider.GetRequiredService<IOptions<NotificationDefinitionStoreOptions>>().Value;
        if (!options.IsDynamicNotificationStoreEnabled)
        {
            return;
        }

        var logger = context.ServiceProvider.GetRequiredService<ILogger<AbpNotificationsDefinitionStoreModule>>();

        if (!context.ServiceProvider.GetRequiredService<IOptions<PermissionManagementOptions>>().Value
                .IsDynamicPermissionStoreEnabled)
        {
            logger.LogWarning(
                "NotificationDefinitionStoreOptions.IsDynamicNotificationStoreEnabled is on but " +
                "PermissionManagementOptions.IsDynamicPermissionStoreEnabled is off: a permission that another service " +
                "defines is unknown to this process, so the recipients of a notification that requires it are filtered " +
                "out as not granted. Turn on IsDynamicPermissionStoreEnabled in this service.");
        }

        if (!context.ServiceProvider.GetRequiredService<IOptions<FeatureManagementOptions>>().Value
                .IsDynamicFeatureStoreEnabled)
        {
            logger.LogWarning(
                "NotificationDefinitionStoreOptions.IsDynamicNotificationStoreEnabled is on but " +
                "FeatureManagementOptions.IsDynamicFeatureStoreEnabled is off: a feature that another service defines is " +
                "unknown to this process, so the recipients of a notification that requires it are filtered out as if " +
                "the feature were disabled. Turn on IsDynamicFeatureStoreEnabled in this service.");
        }
    }
}
