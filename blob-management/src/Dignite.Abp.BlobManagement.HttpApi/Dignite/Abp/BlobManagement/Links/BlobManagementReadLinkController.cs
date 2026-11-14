using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.BlobManagement.Links;

/// <summary>
/// A browser's read with a link: <c>GET|HEAD /api/blob-management/links/{link}</c>. Anonymous - the link is the
/// credential, and it is left out of the audit log.
/// <para>
/// <b>Caching.</b> <c>Cache-Control: private</c>, for no longer than the link has left: the response belongs to the
/// user the link was handed to, and no shared cache keeps it. The <c>ETag</c> is the file's physical BLOB, and a
/// matching <c>If-None-Match</c> is answered <c>304</c> without reading the bytes. The same file within one window is
/// the same link (<c>BlobManagementReadLinkOptions.LinkWindow</c>), so the browser's cache serves it again.
/// </para>
/// <para>
/// <b>Safety</b>, as on the public read: <c>X-Content-Type-Options: nosniff</c> always, and a type outside
/// <see cref="BlobManagementFileTypes"/> is served as an attachment, never rendered on Blob Management's origin. An
/// address for browsers, not an API: left out of ABP's API definition.
/// </para>
/// </summary>
[Area(BlobManagementRemoteServiceConsts.ModuleName)]
[ApiExplorerSettings(IgnoreApi = true)]
[Route(BlobManagementReadLinkUrl.RoutePrefix)]
[AllowAnonymous]
[DisableAuditing]
public class BlobManagementReadLinkController : BlobManagementController
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

        var info = result.File;
        SetCacheHeaders(new EntityTagHeaderValue("\"" + info.EntityTag + "\""), result.ExpirationTime);
        if (result.NotModified)
        {
            result.Content?.Dispose();
            return StatusCode(StatusCodes.Status304NotModified);
        }

        SetContentDisposition(info.MimeType, info.Name);
        if (isHead || result.Content == null)
        {
            result.Content?.Dispose();
            Response.ContentType = info.MimeType;
            Response.ContentLength = info.Size;
            return new EmptyResult();
        }

        return new FileStreamResult(result.Content.GetStream(), info.MimeType);
    }

    protected virtual BlobManagementPublicReadRequest CreateReadRequest(bool headersOnly)
    {
        var request = new BlobManagementPublicReadRequest { HeadersOnly = headersOnly };
        var ifNoneMatch = Request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch == null)
        {
            return request;
        }

        foreach (var candidate in ifNoneMatch)
        {
            if (candidate.Equals(EntityTagHeaderValue.Any))
            {
                request.IfNoneMatchAny = true;
                continue;
            }

            var tag = candidate.Tag.ToString();
            if (tag.Length >= 2 && tag[0] == '"' && tag[^1] == '"')
            {
                tag = tag.Substring(1, tag.Length - 2);
            }

            request.IfNoneMatch.Add(tag);
        }

        return request;
    }

    /// <summary>Private, and for no longer than the link has left.</summary>
    protected virtual void SetCacheHeaders(EntityTagHeaderValue entityTag, DateTime expirationTime)
    {
        var remaining = expirationTime - DateTime.UtcNow;
        var headers = Response.GetTypedHeaders();
        headers.ETag = entityTag;
        headers.CacheControl = new CacheControlHeaderValue
        {
            Private = true,
            MaxAge = remaining > TimeSpan.Zero ? TimeSpan.FromSeconds(Math.Floor(remaining.TotalSeconds)) : TimeSpan.Zero
        };

        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
    }

    protected virtual void SetContentDisposition(string mimeType, string name)
    {
        var known = BlobManagementFileTypes.All.Any(type => type.MimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase));
        var disposition = new ContentDispositionHeaderValue(known ? "inline" : "attachment");
        var slash = name.LastIndexOf('/');
        var fileName = slash < 0 ? name : name[(slash + 1)..];
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            disposition.SetHttpFileName(fileName);
        }

        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
    }
}
