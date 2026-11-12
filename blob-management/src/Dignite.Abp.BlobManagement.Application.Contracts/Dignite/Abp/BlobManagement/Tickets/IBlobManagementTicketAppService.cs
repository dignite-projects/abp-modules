namespace Dignite.Abp.BlobManagement.Tickets;

/// <summary>
/// <see cref="IBlobManagementTicketService"/> for remote callers, behind the same authorization as the BLOB API: an OAuth client,
/// never a user, allowed in the container. A separate contract for the reason <c>IBlobManagementBlobAppService</c> is one.
/// </summary>
public interface IBlobManagementTicketAppService : IBlobManagementTicketService
{
}
