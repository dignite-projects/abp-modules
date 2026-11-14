using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Dignite.Abp.BlobManagement.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.BlobManagement.Links;

/// <summary>
/// A browser's read with a link: <c>GET|HEAD /api/blob-management/links/{link}</c>. Anonymous - the link is the
/// credential, and it is left out of the audit log. Conditional and range requests and the safety headers are
/// <see cref="BlobManagementFileReadController"/>'s. An address for browsers, not an API: left out of ABP's API
/// definition.
/// <para>
/// <b>Caching.</b> <c>Cache-Control: private</c>, for no longer than the link has left: the response belongs to the
/// user the link was handed to, and no shared cache keeps it. The <c>ETag</c> is the file's physical BLOB. The same
/// file within one window is the same link (<c>BlobManagementReadLinkOptions.LinkWindow</c>), so the browser's cache
/// serves it again.
/// </para>
/// </summary>
[Area(BlobManagementRemoteServiceConsts.ModuleName)]
[ApiExplorerSettings(IgnoreApi = true)]
[Route(BlobManagementReadLinkUrl.RoutePrefix)]
[AllowAnonymous]
[DisableAuditing]
[EnableRateLimiting(BlobManagementRateLimiting.ReadLinks)]
public class BlobManagementReadLinkController : BlobManagementFileReadController
{
    protected IBlobManagementReadLinkAppService ReadLinkAppService { get; }

    public BlobManagementReadLinkController(IBlobManagementReadLinkAppService readLinkAppService)
    {
        ReadLinkAppService = readLinkAppService;
    }

    [HttpGet]
    [HttpHead]
    [Route("{link}")]
    public virtual async Task<IActionResult> GetAsync(string link)
    {
        var isHead = HttpMethods.IsHead(Request.Method);

        BlobManagementReadLinkResult result;
        try
        {
            result = await ReadLinkAppService.ReadAsync(link, CreateReadRequest(isHead));
        }
        catch (BusinessException exception) when (exception.Code == BlobManagementErrorCodes.ReadLinkInvalid)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }

        SetCacheHeaders(result.ExpirationTime);
        return CreateFileResult(result, isHead, result.File.Size);
    }

    /// <summary>Private, and for no longer than the link has left.</summary>
    protected virtual void SetCacheHeaders(DateTime expirationTime)
    {
        var remaining = expirationTime - DateTime.UtcNow;
        Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Private = true,
            MaxAge = remaining > TimeSpan.Zero ? TimeSpan.FromSeconds(Math.Floor(remaining.TotalSeconds)) : TimeSpan.Zero
        };
    }
}
