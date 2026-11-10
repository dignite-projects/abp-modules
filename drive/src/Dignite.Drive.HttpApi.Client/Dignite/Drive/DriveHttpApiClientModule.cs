using Dignite.Drive.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        // Static proxies (ClientProxies/, generated with `abp generate-proxy -t csharp -m drive --without-contracts`
        // against drive/host), as ABP's own modules ship them.
        context.Services.AddStaticHttpClientProxies(
            typeof(DriveApplicationContractsModule).Assembly,
            DriveRemoteServiceConsts.RemoteServiceName
        );

        // What the BLOB storing provider resolves: the proxy, behind the in-process service's contract.
        context.Services.Replace(ServiceDescriptor.Transient<IDriveBlobService, RemoteDriveBlobService>());

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<DriveHttpApiClientModule>();
        });
    }
}
