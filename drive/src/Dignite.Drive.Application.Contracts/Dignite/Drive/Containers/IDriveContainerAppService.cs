using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Drive.Containers;

/// <summary>The user drives the Drive host declares. System containers are not listed: they are the applications'.</summary>
public interface IDriveContainerAppService : IApplicationService
{
    Task<ListResultDto<DriveContainerDto>> GetListAsync();

    Task<DriveContainerDto> GetAsync(string name);
}
