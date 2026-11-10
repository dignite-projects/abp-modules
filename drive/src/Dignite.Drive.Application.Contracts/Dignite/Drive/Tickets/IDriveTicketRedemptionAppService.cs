using System.Threading.Tasks;
using Dignite.Drive.Blobs;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Dignite.Drive.Tickets;

/// <summary>
/// Redeems tickets, for anyone who holds one - a browser, anonymous. The ticket is the authorization: signed by the
/// Drive, it names the tenant, the container, the file and its issuer, and the issuer is checked again at redemption.
/// An invalid, expired, used or misdirected ticket fails with <see cref="DriveErrorCodes.InvalidTicket"/>, whichever it
/// was.
/// </summary>
public interface IDriveTicketRedemptionAppService : IApplicationService
{
    /// <summary>Stores the file an upload ticket names. A ticket is good for one successful upload.</summary>
    Task<DriveBlobDto> UploadAsync(string ticket, IRemoteStreamContent content);

    /// <summary>The file a download ticket names; reusable until it expires.</summary>
    Task<IRemoteStreamContent> DownloadAsync(string ticket);
}
