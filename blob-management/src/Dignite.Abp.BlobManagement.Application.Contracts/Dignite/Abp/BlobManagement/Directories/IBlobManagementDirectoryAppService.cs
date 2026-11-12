using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Abp.BlobManagement.Directories;

/// <summary>
/// The directories of the containers that allow them, for signed-in users only. What a caller may do with the
/// directories of a place is the permission of the container or, below a restricted directory, of that directory.
/// </summary>
public interface IBlobManagementDirectoryAppService : IApplicationService
{
    Task<BlobManagementDirectoryDto> GetAsync(Guid id);

    /// <summary>The directories of one level that the caller may view, by position.</summary>
    Task<ListResultDto<BlobManagementDirectoryDto>> GetListAsync(GetBlobManagementDirectoriesInput input);

    Task<BlobManagementDirectoryDto> CreateAsync(CreateBlobManagementDirectoryInput input);

    /// <summary>Renames the directory.</summary>
    Task<BlobManagementDirectoryDto> UpdateAsync(Guid id, UpdateBlobManagementDirectoryInput input);

    /// <summary>Moves the directory under another parent of its container, or among its siblings.</summary>
    Task<BlobManagementDirectoryDto> MoveAsync(Guid id, MoveBlobManagementDirectoryInput input);

    /// <summary>Deletes the directory, which must be empty.</summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Says whether the directory has permissions of its own. It takes <c>BlobManagement.ManagePermissions</c>, as
    /// granting on it does.
    /// </summary>
    Task<BlobManagementDirectoryDto> RestrictAsync(Guid id, RestrictBlobManagementDirectoryInput input);
}
