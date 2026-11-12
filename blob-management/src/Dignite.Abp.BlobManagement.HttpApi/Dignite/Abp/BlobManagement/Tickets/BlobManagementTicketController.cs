using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dignite.Abp.BlobManagement.Tickets;

/// <summary>
/// Ticket issuing for remote applications: <c>POST /api/blob-management/tickets</c>, as an OAuth client. Its C# client proxy
/// implements <see cref="IBlobManagementTicketService"/>. Who may issue is decided in <see cref="IBlobManagementTicketAppService"/>.
/// </summary>
[Area(BlobManagementRemoteServiceConsts.ModuleName)]
[RemoteService(Name = BlobManagementRemoteServiceConsts.RemoteServiceName)]
[Route(BlobManagementTicketUrl.RoutePrefix)]
public class BlobManagementTicketController : BlobManagementController, IBlobManagementTicketService
{
    protected IBlobManagementTicketAppService BlobManagementTicketAppService { get; }

    public BlobManagementTicketController(IBlobManagementTicketAppService driveTicketAppService)
    {
        BlobManagementTicketAppService = driveTicketAppService;
    }

    [HttpPost]
    public virtual Task<BlobManagementTicketDto> CreateAsync(CreateBlobManagementTicketInput input)
    {
        return BlobManagementTicketAppService.CreateAsync(input);
    }
}
