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
/// The defaults live next to their contracts in Abstractions and register with <c>TryRegister</c>. The packages that
/// replace them (the Notification Center's store, the Identity permission checker) depend on Abstractions, never on the
/// in-process implementation package, so nothing orders them against it: a replacement registered before the
/// implementation package must survive it, and the implementation package alone falls back to the stateless defaults.
/// </summary>
public class ContractDefaults_Tests
{
    [Fact]
    public async Task Replacements_registered_before_the_implementation_package_are_not_overridden()
    {
        using var application = await AbpApplicationFactory.CreateAsync<ReplacementsBeforeImplementationModule>(
            options => options.UseAutofac());
        await application.InitializeAsync();

        application.ServiceProvider.GetRequiredService<INotificationStore>()
            .ShouldBeSameAs(ReplacementsModule.Store);
        application.ServiceProvider.GetRequiredService<INotificationPermissionChecker>()
            .ShouldBeSameAs(ReplacementsModule.PermissionChecker);

        await application.ShutdownAsync();
    }

    [Fact]
    public async Task The_implementation_package_alone_falls_back_to_the_stateless_defaults()
    {
        using var application = await AbpApplicationFactory.CreateAsync<ImplementationOnlyModule>(
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

    /// <summary>Stands in for NotificationCenter.Domain / Notifications.Identity: depends on Abstractions only.</summary>
    [DependsOn(typeof(AbpNotificationsAbstractionsModule))]
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

    // Module order follows DependsOn: the replacements are registered before the implementation package's own types.
    [DependsOn(
        typeof(ReplacementsModule),
        typeof(AbpNotificationsModule),
        typeof(AbpAutofacModule))]
    public class ReplacementsBeforeImplementationModule : AbpModule
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
        typeof(AbpNotificationsModule),
        typeof(AbpAutofacModule))]
    public class ImplementationOnlyModule : AbpModule
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
