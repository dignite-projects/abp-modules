using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Drive.Containers;

/// <summary>The user drives: <c>/api/drive/containers</c>.</summary>
[Area(DriveRemoteServiceConsts.ModuleName)]
[RemoteService(Name = DriveRemoteServiceConsts.RemoteServiceName)]
[Route("api/drive/containers")]
public class DriveContainerController : DriveController, IDriveContainerAppService
{
    protected IDriveContainerAppService ContainerAppService { get; }

    public DriveContainerController(IDriveContainerAppService containerAppService)
    {
        ContainerAppService = containerAppService;
    }

    [HttpGet]
    public virtual Task<ListResultDto<DriveContainerDto>> GetListAsync()
    {
        return ContainerAppService.GetListAsync();
    }

    [HttpGet]
    [Route("{name}")]
    public virtual Task<DriveContainerDto> GetAsync(string name)
    {
        return ContainerAppService.GetAsync(name);
    }
}
