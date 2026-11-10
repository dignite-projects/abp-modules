using System;
using System.Threading.Tasks;
using Dignite.Drive.Tickets;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.BlobStoring.Drive;

public class DriveTicketClient : IDriveTicketClient, ITransientDependency
{
    protected IServiceProvider ServiceProvider { get; }

    protected IBlobContainerConfigurationProvider ConfigurationProvider { get; }

    protected ICurrentTenant CurrentTenant { get; }

    protected AbpBlobStoringDriveOptions Options { get; }

    public DriveTicketClient(
        IServiceProvider serviceProvider,
        IBlobContainerConfigurationProvider configurationProvider,
        ICurrentTenant currentTenant,
        IOptions<AbpBlobStoringDriveOptions> options)
    {
        ServiceProvider = serviceProvider;
        ConfigurationProvider = configurationProvider;
        CurrentTenant = currentTenant;
        Options = options.Value;
    }

    public virtual Task<DriveTicketDto> CreateUploadTicketAsync(
        string containerName,
        string blobName,
        long? maxSizeInBytes = null,
        bool overrideExisting = false,
        TimeSpan? lifetime = null)
    {
        return CreateAsync(containerName, new CreateDriveTicketInput
        {
            BlobName = blobName,
            Kind = DriveTicketKind.Upload,
            MaxSizeInBytes = maxSizeInBytes,
            OverrideExisting = overrideExisting,
            LifetimeSeconds = ToSeconds(lifetime)
        });
    }

    public virtual Task<DriveTicketDto> CreateDownloadTicketAsync(string containerName, string blobName, TimeSpan? lifetime = null)
    {
        return CreateAsync(containerName, new CreateDriveTicketInput
        {
            BlobName = blobName,
            Kind = DriveTicketKind.Download,
            LifetimeSeconds = ToSeconds(lifetime)
        });
    }

    protected virtual async Task<DriveTicketDto> CreateAsync(string containerName, CreateDriveTicketInput input)
    {
        var configuration = ConfigurationProvider.Get(containerName);
        var drive = configuration.GetDriveConfiguration();
        drive.Validate();
        input.ContainerName = drive.DriveContainer;

        // The tenant ABP's container stores for: the current one when the container is multi-tenant, the host otherwise.
        DriveTicketDto ticket;
        using (CurrentTenant.Change(configuration.IsMultiTenant ? CurrentTenant.Id : null))
        {
            ticket = await GetTicketService().CreateAsync(input);
        }

        ticket.Url = Options.PublicBaseUrl.TrimEnd('/') + ticket.Url;
        return ticket;
    }

    /// <summary>Resolved on use, as the provider resolves the BLOB service: in process or the client proxy.</summary>
    protected virtual IDriveTicketService GetTicketService()
    {
        return ServiceProvider.GetService(typeof(IDriveTicketService)) as IDriveTicketService
               ?? throw new AbpException(
                   "No IDriveTicketService is registered. The host installs Dignite.Drive.Application (the Drive in this process) or Dignite.Drive.HttpApi.Client (a remote Drive).");
    }

    protected static int? ToSeconds(TimeSpan? lifetime)
    {
        return lifetime == null ? null : (int)Math.Ceiling(lifetime.Value.TotalSeconds);
    }
}
