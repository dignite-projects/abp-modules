using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Dignite.Abp.Notifications.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Caching;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Localization.External;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications;

/// <summary>
/// One service of a test deployment: the definition store on the test's shared database and distributed cache, with
/// both switches on. The startup initialization does not run in the background here — a test saves and reads
/// explicitly, so nothing races it.
/// </summary>
[DependsOn(
    typeof(AbpNotificationsEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule)
    )]
public class DefinitionStoreTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var shared = context.Services.GetSingletonInstance<SharedDefinitionStoreInfrastructure>();

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(dbContext => dbContext.DbContextOptions.UseSqlite(shared.Connection));
        });

        context.Services.Replace(ServiceDescriptor.Singleton<IDistributedCache>(shared.Cache));
        Configure<AbpDistributedCacheOptions>(options => options.KeyPrefix = shared.KeyPrefix);

        // Like a host, it leaves the switches alone in a data migration environment (the module turns them off there).
        if (!context.Services.IsDataMigrationEnvironment())
        {
            Configure<NotificationDefinitionStoreOptions>(options =>
            {
                options.SaveStaticNotificationsToDatabase = true;
                options.IsDynamicNotificationStoreEnabled = true;
            });
        }

        context.Services.Replace(
            ServiceDescriptor.Transient<NotificationDynamicInitializer, ForegroundOnlyNotificationDynamicInitializer>());

        // What NotificationDefinitionsChangedRecorder writes: the definition-changed events this application received.
        context.Services.AddSingleton<ReceivedDefinitionChanges>();
    }
}

/// <summary>A publisher that defines the <see cref="PublisherADefinitionProvider"/> notifications.</summary>
[DependsOn(typeof(DefinitionStoreTestModule))]
public class PublisherATestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<PublisherADefinitionProvider>();
    }
}

/// <summary>A second publisher, writing to the same tables.</summary>
[DependsOn(typeof(DefinitionStoreTestModule))]
public class PublisherBTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<PublisherBDefinitionProvider>();
    }
}

/// <summary>
/// A notification service: no definition of its own, and the publishers' localization resources only through the
/// external localization store (as Language Management provides them in a microservice solution).
/// </summary>
[DependsOn(typeof(DefinitionStoreTestModule))]
public class NotificationServiceTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Replace(
            ServiceDescriptor.Singleton<IExternalLocalizationStore, InMemoryExternalLocalizationStore>());
    }
}

/// <summary>A notification service that also defines one of publisher A's notifications itself.</summary>
[DependsOn(typeof(NotificationServiceTestModule))]
public class NotificationServiceWithLocalCopyTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<LocalCopyDefinitionProvider>();
    }
}

/// <summary>Runs the startup initialization only when a test asks for it in the foreground.</summary>
[DisableConventionalRegistration]
public class ForegroundOnlyNotificationDynamicInitializer : NotificationDynamicInitializer
{
    public ForegroundOnlyNotificationDynamicInitializer(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
    }

    public override Task InitializeAsync(bool runInBackground, CancellationToken cancellationToken = default)
    {
        return runInBackground ? Task.CompletedTask : base.InitializeAsync(false, cancellationToken);
    }
}
