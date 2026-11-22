using Dignite.Abp.BlobManagement.Blobs;
using Dignite.Abp.BlobManagement.Files;
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
        // against blob-management/host, which exposes the integration services for it), as ABP's own modules ship them.
        context.Services.AddStaticHttpClientProxies(
            typeof(BlobManagementApplicationContractsModule).Assembly,
            BlobManagementRemoteServiceConsts.RemoteServiceName
        );

        // What the BLOB storing provider and a business system's file library resolve: the proxies of the integration
        // services, behind the in-process services' contracts. Replaced here, after this assembly's types were
        // registered: a generated proxy registers itself under the contract its methods are declared in - the same
        // IBlobManagementBlobService and IBlobManagementFileService - and these must be what a caller gets.
        context.Services.Replace(ServiceDescriptor.Transient<IBlobManagementBlobService, RemoteBlobManagementBlobService>());
        context.Services.Replace(ServiceDescriptor.Transient<IBlobManagementFileService, RemoteBlobManagementFileService>());

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<BlobManagementHttpApiClientModule>();
        });
    }
}
