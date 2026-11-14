using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The files of the managed containers, by id: what a business system's file library calls, for its user - beside
/// <see cref="Blobs.IBlobManagementBlobService"/>, which keeps an application's BLOBs under their names. The host decides
/// which implementation answers: the in-process one when it installs <c>Dignite.Abp.BlobManagement.Application</c> (a
/// trusted caller), the HTTP client proxy when it installs <c>Dignite.Abp.BlobManagement.HttpApi.Client</c> (a client
/// authenticated by its credentials).
/// <para>
/// It authorizes no user: whether the application's user may store, see, rename or remove a file is the application's
/// rule, applied before it calls. Every call works in the current tenant.
/// </para>
/// </summary>
public interface IBlobManagementFileService : IApplicationService
{
    Task<BlobManagementFileDto> GetAsync(Guid id);

    /// <summary>The files of the top of a container, or of one directory, a page at a time.</summary>
    Task<PagedResultDto<BlobManagementFileDto>> GetListAsync(GetBlobManagementFilesInput input);

    /// <summary>Stores a file at the top of a container, or in a directory.</summary>
    Task<BlobManagementFileDto> CreateAsync(CreateBlobManagementFileInput input);

    /// <summary>Renames a file. One addressed by its name is not renamed.</summary>
    Task<BlobManagementFileDto> UpdateAsync(Guid id, UpdateBlobManagementFileInput input);

    /// <summary>Moves a file to another directory of its container, or to its top. One addressed by its name is not moved.</summary>
    Task<BlobManagementFileDto> MoveAsync(Guid id, MoveBlobManagementFileInput input);

    /// <summary>
    /// Marks a file public or not: whether anyone may read it at its public address. One addressed by its name is not
    /// changed here - its application's container declares it, and each save sets it.
    /// </summary>
    Task<BlobManagementFileDto> SetPublicAsync(Guid id, SetBlobManagementFilePublicInput input);

    /// <summary>
    /// Issues an upload ticket: a browser stores one file with it, without signing in, before it expires - the file's
    /// id is given now. The file is pending until <see cref="ConfirmAsync"/>: it is not listed, gets no read link and is
    /// not read publicly, and one never confirmed is deleted after <c>BlobManagementUploadOptions.PendingLifetime</c>.
    /// <see cref="GetAsync"/> shows a pending file with the user it was uploaded for (<c>CreatorId</c>), which the
    /// business system checks against its own current user before it confirms.
    /// </summary>
    Task<BlobManagementUploadTicketDto> CreateUploadTicketAsync(CreateBlobManagementUploadTicketInput input);

    /// <summary>
    /// Confirms a file uploaded with a ticket, once the business system has filed it with a record of its own. A file
    /// that is not pending is left as it is.
    /// </summary>
    Task<BlobManagementFileDto> ConfirmAsync(Guid id);

    /// <summary>Deletes a file; a file that is not there is no error.</summary>
    Task DeleteAsync(Guid id);

    /// <summary>The file's bytes - in any container, public or not.</summary>
    Task<IRemoteStreamContent> GetContentAsync(Guid id);
}
