using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Blobs;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobManagement.Tickets;

/// <summary>
/// Ticket issuing for remote applications, behind the BLOB API's authorization (<see cref="BlobManagementClientChecker"/>: an
/// allowed OAuth client, never a user). The ticket names the client, which is checked again when it is redeemed. Exposed
/// only through <c>BlobManagementTicketController</c>.
/// </summary>
[Authorize]
[RemoteService(IsEnabled = false)]
[ExposeServices(typeof(IBlobManagementTicketAppService), typeof(BlobManagementTicketAppService))]
public class BlobManagementTicketAppService : BlobManagementTicketService, IBlobManagementTicketAppService
{
    protected BlobManagementClientChecker ClientChecker { get; }

    public BlobManagementTicketAppService(
        FileDescriptorManager fileDescriptorManager,
        BlobManagementTicketProtector ticketProtector,
        BlobManagementClientChecker clientChecker)
        : base(fileDescriptorManager, ticketProtector)
    {
        ClientChecker = clientChecker;
    }

    public override async Task<BlobManagementTicketDto> CreateAsync(CreateBlobManagementTicketInput input)
    {
        Check.NotNull(input, nameof(input));
        var clientId = await ClientChecker.CheckAsync(input.ContainerName);
        return await CreateAsync(input, clientId);
    }
}
