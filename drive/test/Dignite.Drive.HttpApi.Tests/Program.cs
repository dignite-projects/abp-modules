using Dignite.Drive;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Volo.Abp.AspNetCore.TestBase;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    EnvironmentName = Environments.Staging
});
await builder.RunAbpModuleAsync<DriveHttpApiTestModule>();

public partial class Program
{
}
