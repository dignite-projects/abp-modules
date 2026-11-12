using System;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Tickets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The files of the managed containers: <c>/api/blob-management/files</c>. An upload is a form <c>POST</c> (the file in
/// the field <c>file</c>, with <c>containerName</c> and, to file it in a directory, <c>directoryId</c>); a file's bytes
/// are <c>GET /api/blob-management/files/{id}/content</c>, always as an attachment.
/// </summary>
[Area(BlobManagementRemoteServiceConsts.ModuleName)]
[RemoteService(Name = BlobManagementRemoteServiceConsts.RemoteServiceName)]
[Route("api/blob-management/files")]
public class BlobManagementFileController : BlobManagementController, IBlobManagementFileAppService
{
    protected IBlobManagementFileAppService FileAppService { get; }

    public BlobManagementFileController(IBlobManagementFileAppService fileAppService)
    {
        FileAppService = fileAppService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<BlobManagementFileDto> GetAsync(Guid id)
    {
        return FileAppService.GetAsync(id);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<BlobManagementFileDto>> GetListAsync(GetBlobManagementFilesInput input)
    {
        return FileAppService.GetListAsync(input);
    }

    [HttpPost]
    public virtual Task<BlobManagementFileDto> CreateAsync([FromForm] CreateBlobManagementFileInput input)
    {
        return FileAppService.CreateAsync(input);
    }

    [HttpPost]
    [Route("upload-tickets")]
    public virtual Task<BlobManagementTicketDto> CreateUploadTicketAsync(CreateBlobManagementFileUploadTicketInput input)
    {
        return FileAppService.CreateUploadTicketAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<BlobManagementFileDto> UpdateAsync(Guid id, UpdateBlobManagementFileInput input)
    {
        return FileAppService.UpdateAsync(id, input);
    }

    [HttpPut]
    [Route("{id}/move")]
    public virtual Task<BlobManagementFileDto> MoveAsync(Guid id, MoveBlobManagementFileInput input)
    {
        return FileAppService.MoveAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return FileAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("{id}/content")]
    public virtual async Task<IRemoteStreamContent> GetContentAsync(Guid id)
    {
        var content = await FileAppService.GetContentAsync(id);

        // Saved, never rendered on the BlobManagement's origin: a user's file may be HTML. A client that previews it reads the
        // bytes and shows them itself.
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
