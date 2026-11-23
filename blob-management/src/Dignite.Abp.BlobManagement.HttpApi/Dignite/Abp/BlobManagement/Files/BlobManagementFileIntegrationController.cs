using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The file API - <c>/integration-api/blob-management/files</c>: the integration entry a business system's file
/// library calls, as an OAuth client. An ABP integration service, so it is there only where the host exposes
/// integration services - a Blob Management that runs as a service of its own - and is for back ends, never for
/// browsers. An upload is a form <c>POST</c> (the file in the field <c>file</c>, with <c>containerName</c> and, to file
/// it in a directory, <c>directoryId</c>); a file's bytes are
/// <c>GET /integration-api/blob-management/files/{id}/content</c>, always as an attachment. A host that installs
/// <c>Dignite.Abp.BlobManagement.HttpApi.Client</c> keeps files through its C# client proxy, behind
/// <see cref="IBlobManagementFileService"/>. Who may call is decided in
/// <see cref="IBlobManagementFileIntegrationService"/>, not here.
/// </summary>
[Area(BlobManagementRemoteServiceConsts.ModuleName)]
[RemoteService(Name = BlobManagementRemoteServiceConsts.RemoteServiceName)]
[Route("integration-api/blob-management/files")]
public class BlobManagementFileIntegrationController : BlobManagementController, IBlobManagementFileIntegrationService
{
    protected IBlobManagementFileIntegrationService FileIntegrationService { get; }

    public BlobManagementFileIntegrationController(IBlobManagementFileIntegrationService fileIntegrationService)
    {
        FileIntegrationService = fileIntegrationService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<BlobManagementFileDto> GetAsync(Guid id)
    {
        return FileIntegrationService.GetAsync(id);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<BlobManagementFileDto>> GetListAsync(GetBlobManagementFilesInput input)
    {
        return FileIntegrationService.GetListAsync(input);
    }

    [HttpPost]
    public virtual Task<BlobManagementFileDto> CreateAsync([FromForm] CreateBlobManagementFileInput input)
    {
        return FileIntegrationService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<BlobManagementFileDto> UpdateAsync(Guid id, UpdateBlobManagementFileInput input)
    {
        return FileIntegrationService.UpdateAsync(id, input);
    }

    [HttpPut]
    [Route("{id}/move")]
    public virtual Task<BlobManagementFileDto> MoveAsync(Guid id, MoveBlobManagementFileInput input)
    {
        return FileIntegrationService.MoveAsync(id, input);
    }

    [HttpPut]
    [Route("{id}/public")]
    public virtual Task<BlobManagementFileDto> SetPublicAsync(Guid id, SetBlobManagementFilePublicInput input)
    {
        return FileIntegrationService.SetPublicAsync(id, input);
    }

    [HttpPost]
    [Route("upload-tickets")]
    public virtual Task<BlobManagementUploadTicketDto> CreateUploadTicketAsync(CreateBlobManagementUploadTicketInput input)
    {
        return FileIntegrationService.CreateUploadTicketAsync(input);
    }

    [HttpPost]
    [Route("{id}/confirm")]
    public virtual Task<BlobManagementFileDto> ConfirmAsync(Guid id)
    {
        return FileIntegrationService.ConfirmAsync(id);
    }

    [HttpPost]
    [Route("{id}/read-link")]
    public virtual Task<BlobManagementReadLinkDto> CreateReadLinkAsync(Guid id, CreateBlobManagementReadLinkInput input)
    {
        return FileIntegrationService.CreateReadLinkAsync(id, input);
    }

    [HttpPost]
    [Route("read-links")]
    public virtual Task<ListResultDto<BlobManagementReadLinkDto>> CreateReadLinksAsync(CreateBlobManagementReadLinksInput input)
    {
        return FileIntegrationService.CreateReadLinksAsync(input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id, [FromQuery] bool permanently = false)
    {
        return FileIntegrationService.DeleteAsync(id, permanently);
    }

    [HttpPost]
    [Route("{id}/restore")]
    public virtual Task<BlobManagementFileDto> RestoreAsync(Guid id)
    {
        return FileIntegrationService.RestoreAsync(id);
    }

    [HttpGet]
    [Route("{id}/content")]
    public virtual async Task<IRemoteStreamContent> GetContentAsync(Guid id)
    {
        var content = await FileIntegrationService.GetContentAsync(id);

        // Saved, never rendered on Blob Management's origin: a file may be HTML.
        var disposition = new ContentDispositionHeaderValue("attachment");
        if (!string.IsNullOrWhiteSpace(content.FileName))
        {
            disposition.SetHttpFileName(content.FileName);
        }

        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        return content;
    }
}
