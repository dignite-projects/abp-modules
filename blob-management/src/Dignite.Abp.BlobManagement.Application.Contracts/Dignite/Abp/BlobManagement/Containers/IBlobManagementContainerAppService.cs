using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Abp.BlobManagement.Containers;

/// <summary>The managed containers the Blob Management host declares, for signed-in users.</summary>
public interface IBlobManagementContainerAppService : IApplicationService
{
    /// <summary>
    /// The containers the caller has something to do in: every one for a holder of the management permission, else
    /// those the caller holds a grant on.
    /// </summary>
    Task<ListResultDto<BlobManagementContainerDto>> GetListAsync();

    /// <summary>
    /// One container, with its limits and what the caller may do with its directories. Open to every signed-in user:
    /// a business system's file input needs the limits before it offers an upload.
    /// </summary>
    Task<BlobManagementContainerDto> GetAsync(string name);
}
