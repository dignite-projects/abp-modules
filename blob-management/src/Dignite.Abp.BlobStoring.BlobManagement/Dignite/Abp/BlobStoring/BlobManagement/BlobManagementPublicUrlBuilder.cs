using Dignite.Abp.BlobManagement;
using Microsoft.Extensions.Options;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.BlobStoring.BlobManagement;

public class BlobManagementPublicUrlBuilder : IBlobManagementPublicUrlBuilder, ITransientDependency
{
    protected IBlobContainerConfigurationProvider ConfigurationProvider { get; }

    protected ICurrentTenant CurrentTenant { get; }

    protected AbpBlobStoringBlobManagementOptions Options { get; }

    public BlobManagementPublicUrlBuilder(
        IBlobContainerConfigurationProvider configurationProvider,
        ICurrentTenant currentTenant,
        IOptions<AbpBlobStoringBlobManagementOptions> options)
    {
        ConfigurationProvider = configurationProvider;
        CurrentTenant = currentTenant;
        Options = options.Value;
    }

    public virtual string Build(string containerName, string blobName, int? width = null, int? height = null)
    {
        var configuration = ConfigurationProvider.Get(containerName);
        var drive = configuration.GetBlobManagementConfiguration();
        drive.Validate();

        // The tenant the file was stored under: ABP's container stores for the current tenant only when it is
        // multi-tenant, and for the host otherwise.
        var tenantId = configuration.IsMultiTenant ? CurrentTenant.Id : null;

        return Options.PublicBaseUrl.TrimEnd('/') + BlobManagementPublicUrl.Build(drive.BlobManagementContainer, blobName, tenantId, width, height);
    }
}
