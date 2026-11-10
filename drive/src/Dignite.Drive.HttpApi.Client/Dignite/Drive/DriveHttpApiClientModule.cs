using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Drive;

[DependsOn(
    typeof(DriveApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class DriveHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(DriveApplicationContractsModule).Assembly,
            DriveRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<DriveHttpApiClientModule>();
        });

    }
}
