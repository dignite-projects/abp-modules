using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Validation;

namespace Dignite.Drive.Public;

/// <summary>
/// Public reads: <c>GET|HEAD /api/drive/public/{containerName}/{blobName}</c>, optionally <c>?Width=&amp;Height=</c>, and
/// <c>?__tenant=</c> to name the tenant of an anonymous request (see <see cref="DrivePublicUrl"/>). An address, not an
/// API: it is left out of ABP's API definition, so no client proxy is generated for it.
/// <para>
/// <b>Caching.</b> The <c>ETag</c> is the file's <see cref="DrivePublicFileDto.ContentId"/> plus the size asked; it
/// changes when the file is overwritten. A matching <c>If-None-Match</c> is answered <c>304</c> after the lookup,
/// without reading the bytes. <c>Cache-Control</c> is public for a day rather than <c>immutable</c>: a file can be
/// overwritten or deleted, and a day bounds how long a shared cache serves the old one.
/// </para>
/// <para>
/// <b>Safety.</b> <c>X-Content-Type-Options: nosniff</c> always; a type outside <see cref="DriveFileTypes"/> (the
/// curated list without SVG, HTML or scripts - what a container on the any-type preset can hold) is served as an
/// attachment, never rendered on the Drive's origin. No cookie is set on a response meant for a shared cache.
/// </para>
/// </summary>
[Area(DriveRemoteServiceConsts.ModuleName)]
[ApiExplorerSettings(IgnoreApi = true)]
[Route(DrivePublicUrl.RoutePrefix)]
public class DrivePublicController : DriveController
{
    public const int MaxAgeSeconds = 24 * 60 * 60;

    protected IDrivePublicAppService DrivePublicAppService { get; }

    public DrivePublicController(IDrivePublicAppService drivePublicAppService)
    {
        DrivePublicAppService = drivePublicAppService;
    }

    [HttpGet]
    [HttpHead]
    [Route("{containerName}/{*blobName}")]
    public virtual async Task<IActionResult> GetAsync(string containerName, string blobName, [FromQuery] DrivePublicReadInput input)
    {
        DrivePublicFileDto info;
        try
        {
            info = await DrivePublicAppService.GetInfoAsync(containerName, blobName);
        }
        catch (EntityNotFoundException)
        {
            // An address answers 404 itself: ABP's exception filter only shapes errors of object-returning (API) actions.
            return NotFound();
        }

        var entityTag = CreateEntityTag(info, input);
        SetCacheHeaders(entityTag);
        if (IsNotModified(entityTag))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        SetContentDisposition(info.MimeType, FileName(info.Name));
        if (HttpMethods.IsHead(Request.Method))
        {
            // The headers without the bytes: nothing is read from storage, nothing is rendered.
            Response.ContentType = info.MimeType;
            if ((input.Width ?? 0) <= 0 && (input.Height ?? 0) <= 0)
            {
                Response.ContentLength = info.Size;
            }

            return new EmptyResult();
        }

        IRemoteStreamContent content;
        try
        {
            content = await DrivePublicAppService.GetAsync(containerName, blobName, input);
        }
        catch (AbpValidationException)
        {
            // A size beyond DrivePublicUrl.MaxDimension.
            return BadRequest();
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }

        return new FileStreamResult(content.GetStream(), content.ContentType ?? info.MimeType);
    }

    protected virtual string FileName(string name)
    {
        var slash = name.LastIndexOf('/');
        return slash < 0 ? name : name.Substring(slash + 1);
    }

    protected virtual EntityTagHeaderValue CreateEntityTag(DrivePublicFileDto info, DrivePublicReadInput input)
    {
        var size = (input.Width ?? 0) > 0 || (input.Height ?? 0) > 0
            ? "-" + (input.Width ?? 0).ToString(CultureInfo.InvariantCulture) + "x" + (input.Height ?? 0).ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        // Quoted-string content may not contain '"' or '\'; a content id is generated, but be strict anyway.
        var opaque = (info.ContentId + size).Replace("\"", string.Empty).Replace("\\", string.Empty);
        return new EntityTagHeaderValue("\"" + opaque + "\"");
    }

    protected virtual bool IsNotModified(EntityTagHeaderValue entityTag)
    {
        var ifNoneMatch = Request.GetTypedHeaders().IfNoneMatch;
        return ifNoneMatch != null && ifNoneMatch.Any(candidate =>
            candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(entityTag, useStrongComparison: false));
    }

    protected virtual void SetCacheHeaders(EntityTagHeaderValue entityTag)
    {
        var headers = Response.GetTypedHeaders();
        headers.ETag = entityTag;
        headers.CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = TimeSpan.FromSeconds(MaxAgeSeconds)
        };

        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";

        // A cache may refuse a response with Set-Cookie, or store it and hand the cookie to every visitor after. The
        // host's tenant and localization middleware append theirs before this action runs.
        Response.Headers.Remove(HeaderNames.SetCookie);
    }

    protected virtual void SetContentDisposition(string mimeType, string? fileName)
    {
        var known = DriveFileTypes.All.Any(type => type.MimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase));
        var disposition = new ContentDispositionHeaderValue(known ? "inline" : "attachment");
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            disposition.SetHttpFileName(fileName);
        }

        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
    }
}
