using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Abp.BlobManagement.Directories;

/// <summary>The directories of the containers that allow them: <c>/api/blob-management/directories</c>.</summary>
[Area(BlobManagementRemoteServiceConsts.ModuleName)]
[RemoteService(Name = BlobManagementRemoteServiceConsts.RemoteServiceName)]
[Route("api/blob-management/directories")]
public class BlobManagementDirectoryController : BlobManagementController, IBlobManagementDirectoryAppService
{
    protected IBlobManagementDirectoryAppService DirectoryAppService { get; }

    public BlobManagementDirectoryController(IBlobManagementDirectoryAppService directoryAppService)
    {
        DirectoryAppService = directoryAppService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<BlobManagementDirectoryDto> GetAsync(Guid id)
    {
        return DirectoryAppService.GetAsync(id);
    }

    [HttpGet]
    public virtual Task<ListResultDto<BlobManagementDirectoryDto>> GetListAsync(GetBlobManagementDirectoriesInput input)
    {
        return DirectoryAppService.GetListAsync(input);
    }

    [HttpPost]
    public virtual Task<BlobManagementDirectoryDto> CreateAsync(CreateBlobManagementDirectoryInput input)
    {
        return DirectoryAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<BlobManagementDirectoryDto> UpdateAsync(Guid id, UpdateBlobManagementDirectoryInput input)
    {
        return DirectoryAppService.UpdateAsync(id, input);
    }

    [HttpPut]
    [Route("{id}/move")]
    public virtual Task<BlobManagementDirectoryDto> MoveAsync(Guid id, MoveBlobManagementDirectoryInput input)
    {
        return DirectoryAppService.MoveAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return DirectoryAppService.DeleteAsync(id);
    }

    [HttpPut]
    [Route("{id}/restriction")]
    public virtual Task<BlobManagementDirectoryDto> RestrictAsync(Guid id, RestrictBlobManagementDirectoryInput input)
    {
        return DirectoryAppService.RestrictAsync(id, input);
    }
}
