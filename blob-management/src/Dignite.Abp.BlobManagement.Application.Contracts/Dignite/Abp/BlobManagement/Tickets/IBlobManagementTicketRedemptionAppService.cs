using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Blobs;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Tickets;

/// <summary>
/// Redeems tickets, for anyone who holds one - a browser, anonymous. The ticket is the authorization: signed by the
/// Blob Management, it names the tenant, the container, the file and its issuer, and the issuer is checked again at redemption.
/// An invalid, expired, used or misdirected ticket fails with <see cref="BlobManagementErrorCodes.InvalidTicket"/>, whichever it
/// was.
/// </summary>
public interface IBlobManagementTicketRedemptionAppService : IApplicationService
{
    /// <summary>Stores the file an upload ticket names. A ticket is good for one successful upload.</summary>
    Task<BlobManagementBlobDto> UploadAsync(string ticket, IRemoteStreamContent content);

    /// <summary>The file a download ticket names; reusable until it expires.</summary>
    Task<IRemoteStreamContent> DownloadAsync(string ticket);

    /// <summary>
    /// Adds the file to the user drive a file-upload ticket names, as the user who issued it: their permissions there
    /// now, not when they issued it, decide. A ticket is good for one successful upload.
    /// </summary>
    Task<BlobManagementFileDto> UploadFileAsync(string ticket, IRemoteStreamContent content);
}
