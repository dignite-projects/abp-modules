using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Push;
using Dignite.Abp.Notifications.Push.Expo;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The push packages wired the way a host installs them: the contracts plus the push channel plus the Expo provider, and no
/// device store — so the null store must be what answers.
/// </summary>
public class PushModuleRegistration_Tests
{
    [DependsOn(
        typeof(AbpNotificationsAbstractionsModule),
        typeof(AbpNotificationsPushExpoModule),
        typeof(AbpAutofacModule))]
    public class PushTestModule : AbpModule
    {
    }

    [Fact]
    public async Task Installing_the_expo_provider_registers_the_push_channel_with_the_null_store()
    {
        using var application = await AbpApplicationFactory.CreateAsync<PushTestModule>(options =>
        {
            options.UseAutofac();
        });
        await application.InitializeAsync();

        using var scope = application.ServiceProvider.CreateScope();
        var services = scope.ServiceProvider;

        services.GetServices<INotificationNotifier>()
            .OfType<PushNotifier>()
            .Count()
            .ShouldBe(1);
        services.GetServices<IPushProvider>()
            .Select(provider => provider.Name)
            .ShouldBe(new[] { ExpoPushProvider.ProviderName });
        services.GetRequiredService<IPushDeviceStore>().ShouldBeOfType<NullPushDeviceStore>();

        var builder = services.GetRequiredService<INotificationPushBuilder>();
        var content = await builder.BuildAsync(new NotificationPushBuildContext(
            new NotificationPayload
            {
                NotificationName = "test",
                Data = new MessageNotificationData("Hello")
            },
            System.Guid.NewGuid(),
            null));
        content.ShouldNotBeNull();
        content.Body.ShouldBe("Hello");

        await application.ShutdownAsync();
    }
}
