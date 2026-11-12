using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Abp.BlobManagement.Containers;

/// <summary>The user drives the Blob Management host declares. System containers are not listed: they are the applications'.</summary>
public interface IBlobManagementContainerAppService : IApplicationService
{
    Task<ListResultDto<BlobManagementContainerDto>> GetListAsync();

    Task<BlobManagementContainerDto> GetAsync(string name);
}
