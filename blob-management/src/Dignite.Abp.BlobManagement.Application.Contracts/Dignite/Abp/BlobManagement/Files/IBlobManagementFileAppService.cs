using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Dignite.Abp.BlobManagement.Tickets;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The files of the user drives, for signed-in users only: upload one, read one's details or bytes, delete one. Each
/// call is authorized against the file, or for an upload the drive it goes to, with the BlobManagement's own rules.
/// </summary>
public interface IBlobManagementFileAppService : IApplicationService
{
    Task<BlobManagementFileDto> GetAsync(Guid id);

    Task<BlobManagementFileDto> CreateAsync(CreateBlobManagementFileInput input);

    /// <summary>
    /// A ticket to add one file to a user drive later, for a client that cannot send the bytes through an API call - an AI
    /// model, for which they would be text it has to write out - but can reach the file from a shell or a browser: it
    /// posts the file to the ticket's address. Authorized now as the upload itself would be, so a ticket is never issued
    /// for an upload that would be refused; and authorized again, as the caller, when it is used.
    /// </summary>
    Task<BlobManagementTicketDto> CreateUploadTicketAsync(CreateBlobManagementFileUploadTicketInput input);

    Task DeleteAsync(Guid id);

    /// <summary>The file's bytes, for a file the caller may see - in any container, public or not.</summary>
    Task<IRemoteStreamContent> GetContentAsync(Guid id);
}
