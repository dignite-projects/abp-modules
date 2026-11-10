using System.Threading.Tasks;
using Dignite.Drive.Blobs;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Drive.Tickets;

/// <summary>
/// Ticket issuing for remote applications, behind the BLOB API's authorization (<see cref="DriveClientChecker"/>: an
/// allowed OAuth client, never a user). The ticket names the client, which is checked again when it is redeemed. Exposed
/// only through <c>DriveTicketController</c>.
/// </summary>
[Authorize]
[RemoteService(IsEnabled = false)]
[ExposeServices(typeof(IDriveTicketAppService), typeof(DriveTicketAppService))]
public class DriveTicketAppService : DriveTicketService, IDriveTicketAppService
{
    protected DriveClientChecker ClientChecker { get; }

    public DriveTicketAppService(
        FileDescriptorManager fileDescriptorManager,
        DriveTicketProtector ticketProtector,
        DriveClientChecker clientChecker)
        : base(fileDescriptorManager, ticketProtector)
    {
        ClientChecker = clientChecker;
    }

    public override async Task<DriveTicketDto> CreateAsync(CreateDriveTicketInput input)
    {
        Check.NotNull(input, nameof(input));
        var clientId = await ClientChecker.CheckAsync(input.ContainerName);
        return await CreateAsync(input, clientId);
    }
}
