using Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Volo.Abp.AspNetCore.TestBase;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    EnvironmentName = Environments.Staging
});
await builder.RunAbpModuleAsync<ClientIdMetadataDocumentsTestModule>();

public partial class Program
{
}
