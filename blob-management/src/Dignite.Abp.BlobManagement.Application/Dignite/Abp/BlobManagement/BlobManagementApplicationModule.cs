using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Containers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobManagement;

[DependsOn(
    typeof(BlobManagementDomainModule),
    typeof(BlobManagementApplicationContractsModule),
    typeof(AbpDddApplicationModule)
    )]
public class BlobManagementApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Upload tickets and read links are signed with the host's key ring; the host decides where it is kept.
        context.Services.AddDataProtection();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        context.ServiceProvider.GetRequiredService<BlobManagementContainerValidator>().Validate();
    }
}
