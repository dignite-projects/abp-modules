using Dignite.Abp.BlobManagement.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Abp.BlobManagement;

[DependsOn(
    typeof(BlobManagementApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class BlobManagementHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Static proxies (ClientProxies/, generated with `abp generate-proxy -t csharp -m blobManagement --without-contracts`
        // against blob-management/host), as ABP's own modules ship them.
        context.Services.AddStaticHttpClientProxies(
            typeof(BlobManagementApplicationContractsModule).Assembly,
            BlobManagementRemoteServiceConsts.RemoteServiceName
        );

        // What the BLOB storing provider resolves: the proxy, behind the in-process service's contract.
        context.Services.Replace(ServiceDescriptor.Transient<IBlobManagementBlobService, RemoteBlobManagementBlobService>());

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<BlobManagementHttpApiClientModule>();
        });
    }
}
