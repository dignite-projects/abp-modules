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
    /// One container, with its limits and what the caller may do at its top. Open to every signed-in user: a file input
    /// needs the limits before it offers an upload, and the upload is authorized when it happens.
    /// </summary>
    Task<BlobManagementContainerDto> GetAsync(string name);
}
