using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// The BLOB API - the integration entry the remote BLOB storing provider calls, as an OAuth client. The file is
/// addressed by the container in the route and the caller's BLOB name in the query string, which may be a path. Its
/// C# client proxy implements <see cref="IBlobManagementBlobService"/>, so a host that installs <c>Dignite.Abp.BlobManagement.HttpApi.Client</c>
/// stores through it. Who may call is decided in <see cref="IBlobManagementBlobAppService"/>, not here.
/// </summary>
[Area(BlobManagementRemoteServiceConsts.ModuleName)]
[RemoteService(Name = BlobManagementRemoteServiceConsts.RemoteServiceName)]
[Route("api/blob-management/blobs")]
public class BlobManagementBlobController : BlobManagementController, IBlobManagementBlobService
{
    protected IBlobManagementBlobAppService BlobManagementBlobAppService { get; }

    public BlobManagementBlobController(IBlobManagementBlobAppService driveBlobAppService)
    {
        BlobManagementBlobAppService = driveBlobAppService;
    }

    [HttpPut]
    [Route("{containerName}")]
    public virtual Task<BlobManagementBlobDto> SaveAsync(
        string containerName,
        [FromQuery] string blobName,
        IRemoteStreamContent content,
        [FromQuery] bool overrideExisting = false,
        [FromQuery] Guid? creatorId = null)
    {
        return BlobManagementBlobAppService.SaveAsync(containerName, blobName, content, overrideExisting, creatorId);
    }

    [HttpGet]
    [Route("{containerName}")]
    public virtual Task<IRemoteStreamContent?> GetOrNullAsync(string containerName, [FromQuery] string blobName)
    {
        return BlobManagementBlobAppService.GetOrNullAsync(containerName, blobName);
    }

    [HttpGet]
    [Route("{containerName}/exists")]
    public virtual Task<bool> ExistsAsync(string containerName, [FromQuery] string blobName)
    {
        return BlobManagementBlobAppService.ExistsAsync(containerName, blobName);
    }

    [HttpDelete]
    [Route("{containerName}")]
    public virtual Task<bool> DeleteAsync(string containerName, [FromQuery] string blobName)
    {
        return BlobManagementBlobAppService.DeleteAsync(containerName, blobName);
    }
}
