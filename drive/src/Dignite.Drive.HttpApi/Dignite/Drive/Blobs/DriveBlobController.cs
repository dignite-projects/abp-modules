using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Content;

namespace Dignite.Drive.Blobs;

/// <summary>
/// The BLOB API - the integration entry the remote BLOB storing provider calls, as an OAuth client. The file is
/// addressed by the container in the route and the caller's BLOB name in the query string, which may be a path. Its
/// C# client proxy implements <see cref="IDriveBlobService"/>, so a host that installs <c>Dignite.Drive.HttpApi.Client</c>
/// stores through it. Who may call is decided in <see cref="IDriveBlobAppService"/>, not here.
/// </summary>
[Area(DriveRemoteServiceConsts.ModuleName)]
[RemoteService(Name = DriveRemoteServiceConsts.RemoteServiceName)]
[Route("api/drive/blobs")]
public class DriveBlobController : DriveController, IDriveBlobService
{
    protected IDriveBlobAppService DriveBlobAppService { get; }

    public DriveBlobController(IDriveBlobAppService driveBlobAppService)
    {
        DriveBlobAppService = driveBlobAppService;
    }

    [HttpPut]
    [Route("{containerName}")]
    public virtual Task<DriveBlobDto> SaveAsync(
        string containerName,
        [FromQuery] string blobName,
        IRemoteStreamContent content,
        [FromQuery] bool overrideExisting = false)
    {
        return DriveBlobAppService.SaveAsync(containerName, blobName, content, overrideExisting);
    }

    [HttpGet]
    [Route("{containerName}")]
    public virtual Task<IRemoteStreamContent?> GetOrNullAsync(string containerName, [FromQuery] string blobName)
    {
        return DriveBlobAppService.GetOrNullAsync(containerName, blobName);
    }

    [HttpGet]
    [Route("{containerName}/exists")]
    public virtual Task<bool> ExistsAsync(string containerName, [FromQuery] string blobName)
    {
        return DriveBlobAppService.ExistsAsync(containerName, blobName);
    }

    [HttpDelete]
    [Route("{containerName}")]
    public virtual Task<bool> DeleteAsync(string containerName, [FromQuery] string blobName)
    {
        return DriveBlobAppService.DeleteAsync(containerName, blobName);
    }
}
