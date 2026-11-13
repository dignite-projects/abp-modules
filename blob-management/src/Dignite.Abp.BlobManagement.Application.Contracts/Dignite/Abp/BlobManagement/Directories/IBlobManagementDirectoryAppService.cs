using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Abp.BlobManagement.Directories;

/// <summary>
/// The directories of the containers that allow them, for signed-in users: the one thing Blob Management lets a user
/// change, so that a business system need not build it. What a caller may do is the container's permission - the
/// module-wide one, or the one granted on that container - for every directory in it.
/// </summary>
public interface IBlobManagementDirectoryAppService : IApplicationService
{
    Task<BlobManagementDirectoryDto> GetAsync(Guid id);

    /// <summary>The directories of one level, by position.</summary>
    Task<ListResultDto<BlobManagementDirectoryDto>> GetListAsync(GetBlobManagementDirectoriesInput input);

    Task<BlobManagementDirectoryDto> CreateAsync(CreateBlobManagementDirectoryInput input);

    /// <summary>Renames the directory.</summary>
    Task<BlobManagementDirectoryDto> UpdateAsync(Guid id, UpdateBlobManagementDirectoryInput input);

    /// <summary>Moves the directory under another parent of its container, or among its siblings.</summary>
    Task<BlobManagementDirectoryDto> MoveAsync(Guid id, MoveBlobManagementDirectoryInput input);

    /// <summary>Deletes the directory, which must be empty.</summary>
    Task DeleteAsync(Guid id);
}
