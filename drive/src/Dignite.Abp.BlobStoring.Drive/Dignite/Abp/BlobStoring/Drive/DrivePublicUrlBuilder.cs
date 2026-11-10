using Dignite.Drive;
using Microsoft.Extensions.Options;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.BlobStoring.Drive;

public class DrivePublicUrlBuilder : IDrivePublicUrlBuilder, ITransientDependency
{
    protected IBlobContainerConfigurationProvider ConfigurationProvider { get; }

    protected ICurrentTenant CurrentTenant { get; }

    protected AbpBlobStoringDriveOptions Options { get; }

    public DrivePublicUrlBuilder(
        IBlobContainerConfigurationProvider configurationProvider,
        ICurrentTenant currentTenant,
        IOptions<AbpBlobStoringDriveOptions> options)
    {
        ConfigurationProvider = configurationProvider;
        CurrentTenant = currentTenant;
        Options = options.Value;
    }

    public virtual string Build(string containerName, string blobName, int? width = null, int? height = null)
    {
        var configuration = ConfigurationProvider.Get(containerName);
        var drive = configuration.GetDriveConfiguration();
        drive.Validate();

        // The tenant the file was stored under: ABP's container stores for the current tenant only when it is
        // multi-tenant, and for the host otherwise.
        var tenantId = configuration.IsMultiTenant ? CurrentTenant.Id : null;

        return Options.PublicBaseUrl.TrimEnd('/') + DrivePublicUrl.Build(drive.DriveContainer, blobName, tenantId, width, height);
    }
}
