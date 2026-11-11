using System;
using System.Threading.Tasks;
using Volo.Abp;

namespace Dignite.Drive.Tickets;

/// <summary>
/// The in-process <see cref="IDriveTicketService"/>: an application in the Drive's own host is a trusted issuer, and its
/// tickets name no client. Never exposed over HTTP; remote issuers go through <see cref="DriveTicketAppService"/>.
/// </summary>
[RemoteService(IsEnabled = false)]
public class DriveTicketService : DriveAppService, IDriveTicketService
{
    protected FileDescriptorManager FileDescriptorManager { get; }

    protected DriveTicketProtector TicketProtector { get; }

    public DriveTicketService(FileDescriptorManager fileDescriptorManager, DriveTicketProtector ticketProtector)
    {
        FileDescriptorManager = fileDescriptorManager;
        TicketProtector = ticketProtector;
    }

    public virtual Task<DriveTicketDto> CreateAsync(CreateDriveTicketInput input)
    {
        return CreateAsync(input, clientId: null);
    }

    /// <summary>Signs a ticket for the current tenant, naming <paramref name="clientId"/> as its issuer.</summary>
    protected virtual Task<DriveTicketDto> CreateAsync(CreateDriveTicketInput input, string? clientId)
    {
        Check.NotNull(input, nameof(input));
        Check.NotNullOrWhiteSpace(input.BlobName, nameof(input.BlobName), FileDescriptorConsts.MaxNameLength);

        var drive = FileDescriptorManager.GetDriveContainerConfiguration(input.ContainerName);
        if (drive.Kind != DriveContainerKind.System)
        {
            throw new AbpException(
                $"Tickets are issued for system containers; '{input.ContainerName}' is a user drive, which the file API serves.");
        }

        var upload = input.Kind == DriveTicketKind.Upload;
        var ticket = new DriveTicket
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
        return Task.FromResult(new DriveTicketDto
        {
            Ticket = protectedTicket,
            Kind = ticket.Kind,
            ExpiresAt = ticket.ExpiresAt,
            Url = DriveTicketUrl.Build(upload, protectedTicket)
        });
    }
}
