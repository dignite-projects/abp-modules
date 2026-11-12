using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Blobs;
using Dignite.Abp.BlobManagement.Files;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Tickets;

/// <summary>
/// Where a browser redeems a ticket: <c>POST /api/blob-management/tickets/upload?ticket=</c> with the file as the form field
/// <c>content</c>, <c>POST /api/blob-management/tickets/upload-file?ticket=</c> the same way for a user's ticket into a user drive,
/// and <c>GET /api/blob-management/tickets/download?ticket=</c>. Anonymous - the ticket is the authorization - and
/// exempt from antiforgery validation, which a signed, single-purpose ticket makes redundant (a browser that has a
/// cookie of the BlobManagement's host would otherwise be asked for a token it does not have). Addresses for a browser, not an
/// API: left out of ABP's API definition.
/// </summary>
[Area(BlobManagementRemoteServiceConsts.ModuleName)]
[ApiExplorerSettings(IgnoreApi = true)]
[IgnoreAntiforgeryToken]
[Route(BlobManagementTicketUrl.RoutePrefix)]
public class BlobManagementTicketRedemptionController : BlobManagementController, IBlobManagementTicketRedemptionAppService
{
    protected IBlobManagementTicketRedemptionAppService RedemptionAppService { get; }

    public BlobManagementTicketRedemptionController(IBlobManagementTicketRedemptionAppService redemptionAppService)
    {
        RedemptionAppService = redemptionAppService;
    }

    [HttpPost]
    [Route("upload")]
    public virtual Task<BlobManagementBlobDto> UploadAsync([FromQuery] string ticket, IRemoteStreamContent content)
    {
        return RedemptionAppService.UploadAsync(ticket, content);
    }

    [HttpPost]
    [Route("upload-file")]
    public virtual Task<BlobManagementFileDto> UploadFileAsync([FromQuery] string ticket, IRemoteStreamContent content)
    {
        return RedemptionAppService.UploadFileAsync(ticket, content);
    }

    [HttpGet]
    [Route("download")]
    public virtual async Task<IRemoteStreamContent> DownloadAsync([FromQuery] string ticket)
    {
        var content = await RedemptionAppService.DownloadAsync(ticket);

        // Saved, never rendered on the BlobManagement's origin; and a ticket's address is not for a shared cache.
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
