using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Abp.BlobManagement.Containers;

/// <summary>The managed containers: <c>/api/blob-management/containers</c>.</summary>
[Area(BlobManagementRemoteServiceConsts.ModuleName)]
[RemoteService(Name = BlobManagementRemoteServiceConsts.RemoteServiceName)]
[Route("api/blob-management/containers")]
public class BlobManagementContainerController : BlobManagementController, IBlobManagementContainerAppService
{
    protected IBlobManagementContainerAppService ContainerAppService { get; }

    public BlobManagementContainerController(IBlobManagementContainerAppService containerAppService)
    {
        ContainerAppService = containerAppService;
    }

    [HttpGet]
    public virtual Task<ListResultDto<BlobManagementContainerDto>> GetListAsync()
    {
        return ContainerAppService.GetListAsync();
    }

    [HttpGet]
    [Route("{name}")]
    public virtual Task<BlobManagementContainerDto> GetAsync(string name)
    {
        return ContainerAppService.GetAsync(name);
    }
}
