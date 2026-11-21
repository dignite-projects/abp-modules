using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Abp.BlobManagement.Containers;

/// <summary>
/// The managed containers the Blob Management host declares: the list for signed-in users, one container's limits for
/// any authenticated caller.
/// </summary>
public interface IBlobManagementContainerAppService : IApplicationService
{
    /// <summary>
    /// The containers the caller has something to do in: every one for a holder of the management permission, else
    /// those the caller holds a grant on.
    /// </summary>
    Task<ListResultDto<BlobManagementContainerDto>> GetListAsync();

    /// <summary>
    /// One container, with its limits and what the caller may do with its directories. Open to every authenticated
    /// caller: a business system's file input needs the limits before it offers an upload, whether the browser asks
    /// with its user's token or the business system's back end with its client's - whose <c>access</c> is none.
    /// </summary>
    Task<BlobManagementContainerDto> GetAsync(string name);
}
