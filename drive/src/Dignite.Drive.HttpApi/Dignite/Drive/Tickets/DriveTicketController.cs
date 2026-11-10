using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Drive.Tickets;

/// <summary>
/// Ticket issuing for remote applications: <c>POST /api/drive/tickets</c>, as an OAuth client. Its C# client proxy
/// implements <see cref="IDriveTicketService"/>. Who may issue is decided in <see cref="IDriveTicketAppService"/>.
/// </summary>
[Area(DriveRemoteServiceConsts.ModuleName)]
[RemoteService(Name = DriveRemoteServiceConsts.RemoteServiceName)]
[Route(DriveTicketUrl.RoutePrefix)]
public class DriveTicketController : DriveController, IDriveTicketService
{
    protected IDriveTicketAppService DriveTicketAppService { get; }

    public DriveTicketController(IDriveTicketAppService driveTicketAppService)
    {
        DriveTicketAppService = driveTicketAppService;
    }

    [HttpPost]
    public virtual Task<DriveTicketDto> CreateAsync(CreateDriveTicketInput input)
    {
        return DriveTicketAppService.CreateAsync(input);
    }
}
