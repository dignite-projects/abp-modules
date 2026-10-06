using System.Linq;
using Dignite.Abp.Notifications.Push;
using Dignite.NotificationCenter.Push.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.NotificationCenter;

/// <summary>
/// Installing the session package must be enough to switch the push channel to the session-aware store: the null
/// store, then the registry store, are each replaced by the next package in the dependency chain.
/// </summary>
public class PushIdentityModuleRegistration_Tests
{
    [DependsOn(typeof(NotificationCenterPushIdentityModule))]
    public class PushIdentityTestModule : AbpModule
    {
    }

    [Fact]
    public void The_session_aware_store_is_the_push_device_store()
    {
        var services = new ServiceCollection();
        using var application = services.AddApplication<PushIdentityTestModule>();

        services.Where(descriptor => descriptor.ServiceType == typeof(IPushDeviceStore))
            .ShouldHaveSingleItem()
            .ImplementationType.ShouldBe(typeof(IdentitySessionPushDeviceStore));
    }
}
