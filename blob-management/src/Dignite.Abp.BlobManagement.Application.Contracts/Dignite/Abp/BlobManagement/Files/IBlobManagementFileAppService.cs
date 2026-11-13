using System;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Tickets;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The files of the managed containers, for signed-in users only. What a caller may do with files is the permission
/// of the container, at a place the caller may view; nothing is asked of the grant store for each file.
/// </summary>
public interface IBlobManagementFileAppService : IApplicationService
{
    /// <summary>One file by its id: open to every signed-in user, as its bytes are.</summary>
    Task<BlobManagementFileDto> GetAsync(Guid id);

    /// <summary>The files of the top of a container, or of one directory, a page at a time.</summary>
    Task<PagedResultDto<BlobManagementFileDto>> GetListAsync(GetBlobManagementFilesInput input);

    /// <summary>Uploads a file to the top of a container, or into a directory.</summary>
    Task<BlobManagementFileDto> CreateAsync(CreateBlobManagementFileInput input);

    /// <summary>
    /// A ticket to add one file later, for a client that cannot send the bytes through an API call - an AI model, for
    /// which they would be text it has to write out - but can reach the file from a shell or a browser: it posts the
    /// file to the ticket's address. Authorized now as the upload itself would be, so a ticket is never issued for an
    /// upload that would be refused; and authorized again, as the caller, when it is used.
    /// </summary>
    Task<BlobManagementTicketDto> CreateUploadTicketAsync(CreateBlobManagementFileUploadTicketInput input);

    /// <summary>Renames an uploaded file.</summary>
    Task<BlobManagementFileDto> UpdateAsync(Guid id, UpdateBlobManagementFileInput input);

    /// <summary>Moves an uploaded file to another directory of its container, or to its top.</summary>
    Task<BlobManagementFileDto> MoveAsync(Guid id, MoveBlobManagementFileInput input);

    /// <summary>Deletes a file: its uploader may, and whoever may delete files where it is.</summary>
    Task DeleteAsync(Guid id);

    /// <summary>The file's bytes - in any container, public or not.</summary>
    Task<IRemoteStreamContent> GetContentAsync(Guid id);
}
