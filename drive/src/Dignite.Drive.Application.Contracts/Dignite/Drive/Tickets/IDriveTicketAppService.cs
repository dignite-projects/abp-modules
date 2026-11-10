namespace Dignite.Drive.Tickets;

/// <summary>
/// <see cref="IDriveTicketService"/> for remote callers, behind the same authorization as the BLOB API: an OAuth client,
/// never a user, allowed in the container. A separate contract for the reason <c>IDriveBlobAppService</c> is one.
/// </summary>
public interface IDriveTicketAppService : IDriveTicketService
{
}
