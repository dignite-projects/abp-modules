using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Files;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Uploads;

/// <summary>
/// A browser's upload with a ticket (<see cref="IBlobManagementFileService.CreateUploadTicketAsync"/>): anonymous -
/// the ticket is the credential - in the tenant the ticket names. Behind <c>/api/blob-management/uploads/{ticket}</c>;
/// a business system never calls it.
/// </summary>
public interface IBlobManagementUploadAppService : IApplicationService
{
    /// <summary>
    /// What the ticket allows, read before the request body: the endpoint limits the body to
    /// <see cref="BlobManagementUploadTicketInfoDto.MaxSize"/>. An expired, altered or foreign ticket is refused with
    /// <see cref="BlobManagementErrorCodes.UploadTicketInvalid"/>.
    /// </summary>
    Task<BlobManagementUploadTicketInfoDto> GetTicketAsync(string ticket);

    /// <summary>
    /// Stores the file the ticket allows, under the id it names, pending until its business system confirms it. The
    /// ticket's limits and the container's rules both apply. A ticket is used once: a second upload fails with
    /// <see cref="BlobManagementErrorCodes.UploadTicketUsed"/>.
    /// </summary>
    Task<BlobManagementFileDto> UploadAsync(string ticket, IRemoteStreamContent file);
}
