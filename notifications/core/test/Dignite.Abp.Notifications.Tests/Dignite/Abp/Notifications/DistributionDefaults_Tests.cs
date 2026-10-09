using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The packages that replace Distribution's defaults (the Notification Center's store, the Identity permission checker)
/// depend on Core only, so nothing orders them against Distribution. Its defaults are therefore registered with
/// <c>TryRegister</c>: an implementation registered first must survive Distribution's conventional registration.
/// </summary>
public class DistributionDefaults_Tests
{
    [Fact]
    public async Task Implementations_registered_before_distribution_are_not_overridden_by_its_defaults()
    {
        using var application = await AbpApplicationFactory.CreateAsync<ReplacementsBeforeDistributionModule>(
            options => options.UseAutofac());
        await application.InitializeAsync();

        application.ServiceProvider.GetRequiredService<INotificationStore>()
            .ShouldBeSameAs(ReplacementsModule.Store);
        application.ServiceProvider.GetRequiredService<INotificationPermissionChecker>()
            .ShouldBeSameAs(ReplacementsModule.PermissionChecker);

        await application.ShutdownAsync();
    }

    [Fact]
    public async Task Distribution_alone_falls_back_to_the_stateless_defaults()
    {
        using var application = await AbpApplicationFactory.CreateAsync<DistributionOnlyModule>(
            options => options.UseAutofac());
        await application.InitializeAsync();

        application.ServiceProvider.GetRequiredService<INotificationStore>()
            .ShouldBeOfType<NullNotificationStore>();
        application.ServiceProvider.GetRequiredService<INotificationPermissionChecker>()
            .ShouldBeOfType<AlwaysGrantedNotificationPermissionChecker>();
        application.ServiceProvider.GetRequiredService<INotificationPublisher>()
            .ShouldBeOfType<DefaultNotificationPublisher>();

        await application.ShutdownAsync();
    }

    /// <summary>Stands in for NotificationCenter.Domain / Notifications.Identity: depends on Core, not Distribution.</summary>
    [DependsOn(typeof(AbpNotificationsModule))]
    public class ReplacementsModule : AbpModule
    {
        public static readonly INotificationStore Store = Substitute.For<INotificationStore>();

        public static readonly INotificationPermissionChecker PermissionChecker =
            Substitute.For<INotificationPermissionChecker>();

        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            SkipAutoServiceRegistration = true;
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.Replace(ServiceDescriptor.Singleton(Store));
            context.Services.Replace(ServiceDescriptor.Singleton(PermissionChecker));
        }
    }

    // Module order follows DependsOn: the replacements are registered before Distribution's own types.
    [DependsOn(
        typeof(ReplacementsModule),
        typeof(AbpNotificationsDistributionModule),
        typeof(AbpAutofacModule))]
    public class ReplacementsBeforeDistributionModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            SkipAutoServiceRegistration = true;
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<NotificationRoutingOptions>(options => options.Default = new[] { "Test" });
        }
    }

    [DependsOn(
        typeof(AbpNotificationsDistributionModule),
        typeof(AbpAutofacModule))]
    public class DistributionOnlyModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            SkipAutoServiceRegistration = true;
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<NotificationRoutingOptions>(options => options.Default = new[] { "Test" });
        }
    }
}
