using System;
using System.Threading.Tasks;
using Volo.Abp;

namespace Dignite.Abp.BlobManagement.Tickets;

/// <summary>
/// The in-process <see cref="IBlobManagementTicketService"/>: an application in the BlobManagement's own host is a trusted issuer, and its
/// tickets name no client. Never exposed over HTTP; remote issuers go through <see cref="BlobManagementTicketAppService"/>.
/// </summary>
[RemoteService(IsEnabled = false)]
public class BlobManagementTicketService : BlobManagementAppService, IBlobManagementTicketService
{
    protected FileDescriptorManager FileDescriptorManager { get; }

    protected BlobManagementTicketProtector TicketProtector { get; }

    public BlobManagementTicketService(FileDescriptorManager fileDescriptorManager, BlobManagementTicketProtector ticketProtector)
    {
        FileDescriptorManager = fileDescriptorManager;
        TicketProtector = ticketProtector;
    }

    public virtual Task<BlobManagementTicketDto> CreateAsync(CreateBlobManagementTicketInput input)
    {
        return CreateAsync(input, clientId: null);
    }

    /// <summary>Signs a ticket for the current tenant, naming <paramref name="clientId"/> as its issuer.</summary>
    protected virtual Task<BlobManagementTicketDto> CreateAsync(CreateBlobManagementTicketInput input, string? clientId)
    {
        Check.NotNull(input, nameof(input));
        Check.NotNullOrWhiteSpace(input.BlobName, nameof(input.BlobName), FileDescriptorConsts.MaxNameLength);

        FileDescriptorManager.GetBlobManagementContainerConfiguration(input.ContainerName);

        var upload = input.Kind == BlobManagementTicketKind.Upload;
        var ticket = new BlobManagementTicket
        {
            Kind = input.Kind,
            TenantId = CurrentTenant.Id,
            ContainerName = input.ContainerName,
            BlobName = input.BlobName,
            MaxSizeInBytes = upload ? input.MaxSizeInBytes : null,
            OverrideExisting = upload && input.OverrideExisting,
            ClientId = clientId,
            Nonce = Guid.NewGuid().ToString("N"),
            ExpiresAt = TicketProtector.ExpiresAfter(input.LifetimeSeconds)
        };

        var protectedTicket = TicketProtector.Protect(ticket);
        return Task.FromResult(new BlobManagementTicketDto
        {
            Ticket = protectedTicket,
            Kind = ticket.Kind,
            ExpiresAt = ticket.ExpiresAt,
            Url = BlobManagementTicketUrl.Build(upload, protectedTicket)
        });
    }
}
