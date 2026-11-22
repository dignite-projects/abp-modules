using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// The BLOB API - <c>/integration-api/blob-management/blobs</c>: the integration entry the remote BLOB storing provider
/// calls, as an OAuth client. An ABP integration service, so it is there only where the host exposes integration
/// services - a Blob Management that runs as a service of its own - and is for back ends, never for browsers. The file
/// is addressed by the container in the route and the caller's BLOB name in the query string, which may be a path. Its
/// C# client proxy is what <c>Dignite.Abp.BlobManagement.HttpApi.Client</c> stores through. Who may call is decided in
/// <see cref="IBlobManagementBlobIntegrationService"/>, not here.
/// </summary>
[Area(BlobManagementRemoteServiceConsts.ModuleName)]
[RemoteService(Name = BlobManagementRemoteServiceConsts.RemoteServiceName)]
[Route("integration-api/blob-management/blobs")]
public class BlobManagementBlobIntegrationController : BlobManagementController, IBlobManagementBlobIntegrationService
{
    protected IBlobManagementBlobIntegrationService BlobIntegrationService { get; }

    public BlobManagementBlobIntegrationController(IBlobManagementBlobIntegrationService blobIntegrationService)
    {
        BlobIntegrationService = blobIntegrationService;
    }

    [HttpPut]
    [Route("{containerName}")]
    public virtual Task<BlobManagementBlobDto> SaveAsync(
        string containerName,
        [FromQuery] string blobName,
        IRemoteStreamContent content,
        [FromQuery] bool overrideExisting = false,
        [FromQuery] Guid? creatorId = null,
        [FromQuery] bool isPublic = false,
        [FromQuery] string? sha256 = null)
    {
        return BlobIntegrationService.SaveAsync(containerName, blobName, content, overrideExisting, creatorId, isPublic, sha256);
    }

    [HttpGet]
    [Route("{containerName}")]
    public virtual Task<IRemoteStreamContent?> GetOrNullAsync(string containerName, [FromQuery] string blobName)
    {
        return BlobIntegrationService.GetOrNullAsync(containerName, blobName);
    }

    [HttpGet]
    [Route("{containerName}/exists")]
    public virtual Task<bool> ExistsAsync(string containerName, [FromQuery] string blobName)
    {
        return BlobIntegrationService.ExistsAsync(containerName, blobName);
    }

    [HttpDelete]
    [Route("{containerName}")]
    public virtual Task<bool> DeleteAsync(string containerName, [FromQuery] string blobName)
    {
        return BlobIntegrationService.DeleteAsync(containerName, blobName);
    }
}
