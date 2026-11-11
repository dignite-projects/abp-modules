using System;
using System.Threading.Tasks;
using Dignite.Drive.Tickets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp;
using Volo.Abp.Content;

namespace Dignite.Drive.Files;

/// <summary>
/// The files of the user drives: <c>/api/drive/files</c>. An upload is a form <c>POST</c> (the file in the field
/// <c>file</c>); a file's bytes are <c>GET /api/drive/files/{id}/content</c>, always as an attachment.
/// </summary>
[Area(DriveRemoteServiceConsts.ModuleName)]
[RemoteService(Name = DriveRemoteServiceConsts.RemoteServiceName)]
[Route("api/drive/files")]
public class DriveFileController : DriveController, IDriveFileAppService
{
    protected IDriveFileAppService FileAppService { get; }

    public DriveFileController(IDriveFileAppService fileAppService)
    {
        FileAppService = fileAppService;
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<DriveFileDto> GetAsync(Guid id)
    {
        return FileAppService.GetAsync(id);
    }

    [HttpPost]
    public virtual Task<DriveFileDto> CreateAsync([FromForm] CreateDriveFileInput input)
    {
        return FileAppService.CreateAsync(input);
    }

    [HttpPost]
    [Route("upload-tickets")]
    public virtual Task<DriveTicketDto> CreateUploadTicketAsync(CreateDriveFileUploadTicketInput input)
    {
        return FileAppService.CreateUploadTicketAsync(input);
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

        // Saved, never rendered on the Drive's origin: a user's file may be HTML. A client that previews it reads the
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
