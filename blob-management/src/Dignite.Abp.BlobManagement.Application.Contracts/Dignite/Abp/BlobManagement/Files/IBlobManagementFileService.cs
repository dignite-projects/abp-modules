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

    /// <summary>Deletes a file; a file that is not there is no error.</summary>
    Task DeleteAsync(Guid id);

    /// <summary>The file's bytes - in any container, public or not.</summary>
    Task<IRemoteStreamContent> GetContentAsync(Guid id);
}
