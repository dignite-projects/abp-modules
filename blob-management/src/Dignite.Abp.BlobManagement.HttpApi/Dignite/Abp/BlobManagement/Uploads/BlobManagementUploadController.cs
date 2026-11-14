using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Auditing;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Uploads;

/// <summary>
/// A browser's upload with a ticket: <c>POST /api/blob-management/uploads/{ticket}</c>, the file in the multipart form
/// field <c>file</c> - or the raw body, named by <c>?name=</c>. Anonymous, since the ticket is the credential: no
/// antiforgery token is asked for, and no cookie is used (<see cref="IBlobManagementUploadAppService"/> runs under an
/// anonymous principal in the ticket's tenant). Cross-origin browsers need the host's CORS policy to allow their origin.
/// <para>
/// The ticket is read before the body, and the body is limited to the size it allows - the server stops reading past
/// it - besides the limit the service puts on the file itself. The ticket is left out of the audit log: it is a
/// credential. An address for browsers, not an API: it is left out of ABP's API definition, so no client proxy is
/// generated for it.
/// </para>
/// </summary>
[Area(BlobManagementRemoteServiceConsts.ModuleName)]
[ApiExplorerSettings(IgnoreApi = true)]
[Route(BlobManagementUploadUrl.RoutePrefix)]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[DisableAuditing]
public class BlobManagementUploadController : BlobManagementController
{
    /// <summary>What a multipart body adds to the file it carries: boundaries and part headers.</summary>
    public const long MultipartOverhead = 64 * 1024;

    protected IBlobManagementUploadAppService UploadAppService { get; }

    public BlobManagementUploadController(IBlobManagementUploadAppService uploadAppService)
    {
        UploadAppService = uploadAppService;
    }

    [HttpPost]
    [Route("{ticket}")]
    public virtual async Task<BlobManagementFileDto> UploadAsync(string ticket, [FromQuery] string? name = null)
    {
        var info = await UploadAppService.GetTicketAsync(ticket);
        LimitRequestBody(info.MaxSize + MultipartOverhead);

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = info.MaxSize + MultipartOverhead });
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file == null)
            {
                throw new Volo.Abp.UserFriendlyException("The form carries no file.");
            }

            await using var stream = file.OpenReadStream();
            return await UploadAppService.UploadAsync(
                ticket, new RemoteStreamContent(stream, file.FileName, file.ContentType, file.Length, disposeStream: false));
        }

        return await UploadAppService.UploadAsync(
            ticket, new RemoteStreamContent(Request.Body, name, Request.ContentType, Request.ContentLength, disposeStream: false));
    }

    /// <summary>The server's own limit on the body, where it lets an action set one before the body is read.</summary>
    protected virtual void LimitRequestBody(long maxBytes)
    {
        var feature = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = maxBytes;
        }
    }
}
