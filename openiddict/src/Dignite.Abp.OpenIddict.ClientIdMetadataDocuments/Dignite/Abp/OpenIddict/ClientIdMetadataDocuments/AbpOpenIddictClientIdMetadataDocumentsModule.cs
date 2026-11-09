using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>
/// Lets the OpenIddict authorization server accept Client ID Metadata Documents. A host depends on this module and
/// configures <see cref="AbpClientIdMetadataDocumentOptions"/> (at least <see cref="AbpClientIdMetadataDocumentOptions.Scopes"/>);
/// the module wires the rest.
/// </summary>
[DependsOn(
    typeof(AbpOpenIddictAspNetCoreModule),
    typeof(AbpBackgroundWorkersModule)
)]
public class AbpOpenIddictClientIdMetadataDocumentsModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<OpenIddictServerBuilder>(builder =>
        {
            builder.AddEventHandler(ProvisionClientIdMetadataDocumentClient.Descriptor);
            builder.AddEventHandler(AttachClientIdMetadataDocumentSupport.Descriptor);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClient(ClientIdMetadataDocumentFetcher.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(serviceProvider => ClientIdMetadataDocumentFetcher.CreateGuardedHandler(
                serviceProvider.GetRequiredService<IOptions<AbpClientIdMetadataDocumentOptions>>().Value));

        context.Services.AddTransient(serviceProvider => new ClientIdMetadataDocumentFetcher(
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientIdMetadataDocumentFetcher.HttpClientName),
            serviceProvider.GetRequiredService<IOptions<AbpClientIdMetadataDocumentOptions>>().Value));
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var options = context.ServiceProvider.GetRequiredService<IOptions<AbpClientIdMetadataDocumentOptions>>().Value;
        if (!options.IsEnabled)
        {
            return;
        }

        if (options.Scopes.Count == 0)
        {
            throw new AbpException(
                $"{nameof(AbpClientIdMetadataDocumentOptions)}.{nameof(AbpClientIdMetadataDocumentOptions.Scopes)} is empty. " +
                "Add the scopes a client registered from a Client ID Metadata Document may request, such as your API's scope " +
                "and \"profile\", or set IsEnabled to false.");
        }

        if (options.IsCleanupEnabled)
        {
            await context.AddBackgroundWorkerAsync<ClientIdMetadataDocumentCleanupBackgroundWorker>();
        }
    }
}
