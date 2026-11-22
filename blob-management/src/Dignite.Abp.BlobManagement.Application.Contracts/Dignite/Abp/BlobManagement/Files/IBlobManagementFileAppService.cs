using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The files of the managed containers, read by a user who holds the management permission: what the management page
/// shows (<c>api/blob-management/files</c>). It reads and nothing more - no user stores, renames, moves or deletes a
/// file through the module: that is its business system's, under its own rules, through
/// <see cref="IBlobManagementFileService"/>.
/// </summary>
public interface IBlobManagementFileAppService : IApplicationService
{
    Task<BlobManagementFileDto> GetAsync(Guid id);

    /// <summary>The files of the top of a container, or of one directory, a page at a time.</summary>
    Task<PagedResultDto<BlobManagementFileDto>> GetListAsync(GetBlobManagementFilesInput input);

    /// <summary>The file's bytes - in any container, public or not.</summary>
    Task<IRemoteStreamContent> GetContentAsync(Guid id);
}
