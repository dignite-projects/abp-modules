using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.Push.Expo;

[DependsOn(typeof(AbpNotificationsPushModule))]
public class AbpNotificationsPushExpoModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Expo recommends gzip for push requests' responses.
        context.Services
            .AddHttpClient(ExpoPushProvider.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            });
    }
}
