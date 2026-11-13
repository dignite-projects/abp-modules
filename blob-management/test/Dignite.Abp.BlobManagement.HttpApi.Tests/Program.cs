using Dignite.Abp.BlobManagement;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Volo.Abp.AspNetCore.TestBase;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    EnvironmentName = Environments.Staging
});
await builder.RunAbpModuleAsync<BlobManagementHttpApiTestModule>();

public partial class Program
{
}
