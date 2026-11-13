using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Abp.BlobManagement.Tickets;

/// <summary>
/// Issues tickets: an application, having authorized its user by its own rules, lets the user's browser store or read
/// one file in the Blob Management directly, without the bytes passing through the application. What
/// <c>IBlobManagementTicketClient</c> calls; in process or over HTTP as the BLOB service is.
/// <para>
/// The bytes of an upload never pass the application, so its container's own pipeline contributors do not run on them -
/// only the Blob Management container's do. An upload that needs the application's rules cannot use a ticket.
/// </para>
/// </summary>
public interface IBlobManagementTicketService : IApplicationService
{
    Task<BlobManagementTicketDto> CreateAsync(CreateBlobManagementTicketInput input);
}
