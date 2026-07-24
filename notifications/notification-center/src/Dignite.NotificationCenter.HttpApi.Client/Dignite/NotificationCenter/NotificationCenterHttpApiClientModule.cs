using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;

namespace Dignite.NotificationCenter;

[DependsOn(
    typeof(NotificationCenterApplicationContractsModule),
    typeof(AbpHttpClientModule)
    )]
public class NotificationCenterHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(NotificationCenterApplicationContractsModule).Assembly);
        // Application.Contracts depends on AbpNotificationsAbstractionsModule, which owns the single global
        // tolerant NotificationData converter for Core, remote clients, and independently hosted Notifiers.
    }
}
