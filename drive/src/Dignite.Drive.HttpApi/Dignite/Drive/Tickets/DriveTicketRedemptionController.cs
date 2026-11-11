using System.Threading.Tasks;
using Dignite.Drive.Blobs;
using Dignite.Drive.Files;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp.Content;

namespace Dignite.Drive.Tickets;

/// <summary>
/// Where a browser redeems a ticket: <c>POST /api/drive/tickets/upload?ticket=</c> with the file as the form field
/// <c>content</c>, <c>POST /api/drive/tickets/upload-file?ticket=</c> the same way for a user's ticket into a user drive,
/// and <c>GET /api/drive/tickets/download?ticket=</c>. Anonymous - the ticket is the authorization - and
/// exempt from antiforgery validation, which a signed, single-purpose ticket makes redundant (a browser that has a
/// cookie of the Drive's host would otherwise be asked for a token it does not have). Addresses for a browser, not an
/// API: left out of ABP's API definition.
/// </summary>
[Area(DriveRemoteServiceConsts.ModuleName)]
[ApiExplorerSettings(IgnoreApi = true)]
[IgnoreAntiforgeryToken]
[Route(DriveTicketUrl.RoutePrefix)]
public class DriveTicketRedemptionController : DriveController, IDriveTicketRedemptionAppService
{
    protected IDriveTicketRedemptionAppService RedemptionAppService { get; }

    public DriveTicketRedemptionController(IDriveTicketRedemptionAppService redemptionAppService)
    {
        RedemptionAppService = redemptionAppService;
    }

    [HttpPost]
    [Route("upload")]
    public virtual Task<DriveBlobDto> UploadAsync([FromQuery] string ticket, IRemoteStreamContent content)
    {
        return RedemptionAppService.UploadAsync(ticket, content);
    }

    [HttpPost]
    [Route("upload-file")]
    public virtual Task<DriveFileDto> UploadFileAsync([FromQuery] string ticket, IRemoteStreamContent content)
    {
        return RedemptionAppService.UploadFileAsync(ticket, content);
    }

    [HttpGet]
    [Route("download")]
    public virtual async Task<IRemoteStreamContent> DownloadAsync([FromQuery] string ticket)
    {
        var content = await RedemptionAppService.DownloadAsync(ticket);

        // Saved, never rendered on the Drive's origin; and a ticket's address is not for a shared cache.
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
