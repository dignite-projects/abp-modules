using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The files of the management page - <c>/api/blob-management/files</c>: a user who holds the management permission
/// lists a place, reads one file and downloads its bytes (<c>GET /api/blob-management/files/{id}/content</c>, always
/// as an attachment). It reads and changes nothing; a business system keeps its files through the integration entry
/// (<c>BlobManagementFileIntegrationController</c>). Who may call is decided in
/// <see cref="IBlobManagementFileAppService"/>, not here.
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

    [HttpGet]
    [Route("{id}/content")]
    public virtual async Task<IRemoteStreamContent> GetContentAsync(Guid id)
    {
        var content = await FileAppService.GetContentAsync(id);

        // Saved, never rendered on Blob Management's origin: a file may be HTML. A page that previews it reads the bytes
        // and shows them itself.
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
