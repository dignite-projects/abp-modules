using System;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Tickets;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.BlobStoring.BlobManagement;

public class BlobManagementTicketClient : IBlobManagementTicketClient, ITransientDependency
{
    protected IServiceProvider ServiceProvider { get; }

    protected IBlobContainerConfigurationProvider ConfigurationProvider { get; }

    protected ICurrentTenant CurrentTenant { get; }

    protected AbpBlobStoringBlobManagementOptions Options { get; }

    public BlobManagementTicketClient(
        IServiceProvider serviceProvider,
        IBlobContainerConfigurationProvider configurationProvider,
        ICurrentTenant currentTenant,
        IOptions<AbpBlobStoringBlobManagementOptions> options)
    {
        ServiceProvider = serviceProvider;
        ConfigurationProvider = configurationProvider;
        CurrentTenant = currentTenant;
        Options = options.Value;
    }

    public virtual Task<BlobManagementTicketDto> CreateUploadTicketAsync(
        string containerName,
        string blobName,
        long? maxSizeInBytes = null,
        bool overrideExisting = false,
        TimeSpan? lifetime = null)
    {
        return CreateAsync(containerName, new CreateBlobManagementTicketInput
        {
            BlobName = blobName,
            Kind = BlobManagementTicketKind.Upload,
            MaxSizeInBytes = maxSizeInBytes,
            OverrideExisting = overrideExisting,
            LifetimeSeconds = ToSeconds(lifetime)
        });
    }

    public virtual Task<BlobManagementTicketDto> CreateDownloadTicketAsync(string containerName, string blobName, TimeSpan? lifetime = null)
    {
        return CreateAsync(containerName, new CreateBlobManagementTicketInput
        {
            BlobName = blobName,
            Kind = BlobManagementTicketKind.Download,
            LifetimeSeconds = ToSeconds(lifetime)
        });
    }

    protected virtual async Task<BlobManagementTicketDto> CreateAsync(string containerName, CreateBlobManagementTicketInput input)
    {
        var configuration = ConfigurationProvider.Get(containerName);
        var drive = configuration.GetBlobManagementConfiguration();
        drive.Validate();
        input.ContainerName = drive.BlobManagementContainer;

        // The tenant ABP's container stores for: the current one when the container is multi-tenant, the host otherwise.
        BlobManagementTicketDto ticket;
        using (CurrentTenant.Change(configuration.IsMultiTenant ? CurrentTenant.Id : null))
        {
            ticket = await GetTicketService().CreateAsync(input);
        }

        ticket.Url = Options.PublicBaseUrl.TrimEnd('/') + ticket.Url;
        return ticket;
    }

    /// <summary>Resolved on use, as the provider resolves the BLOB service: in process or the client proxy.</summary>
    protected virtual IBlobManagementTicketService GetTicketService()
    {
        return ServiceProvider.GetService(typeof(IBlobManagementTicketService)) as IBlobManagementTicketService
               ?? throw new AbpException(
                   "No IBlobManagementTicketService is registered. The host installs Dignite.Abp.BlobManagement.Application (the BlobManagement in this process) or Dignite.Abp.BlobManagement.HttpApi.Client (a remote BlobManagement).");
    }

    protected static int? ToSeconds(TimeSpan? lifetime)
    {
        return lifetime == null ? null : (int)Math.Ceiling(lifetime.Value.TotalSeconds);
    }
}
