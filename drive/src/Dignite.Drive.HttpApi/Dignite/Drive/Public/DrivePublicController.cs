using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Validation;

namespace Dignite.Drive.Public;

/// <summary>
/// Public reads: <c>GET|HEAD /api/drive/public/{containerName}/{blobName}</c>, optionally <c>?Width=&amp;Height=</c>, and
/// <c>?__tenant=</c> to name the tenant of an anonymous request (see <see cref="DrivePublicUrl"/>). An address, not an
/// API: it is left out of ABP's API definition, so no client proxy is generated for it.
/// <para>
/// <b>Caching.</b> The <c>ETag</c> is the read's <see cref="DrivePublicFileDto.EntityTag"/> - the file's
/// <see cref="DrivePublicFileDto.ContentId"/> plus the size asked; it changes when the file is overwritten. A matching
/// <c>If-None-Match</c> is answered <c>304</c> after the lookup, without reading the bytes. A response is answered from
/// one lookup of the file (<see cref="IDrivePublicAppService.ReadAsync"/>): in a system container a file can be
/// overwritten between two lookups, and a response made from both would store the new bytes in a shared cache under
/// the old file's <c>ETag</c>, and decide inline or attachment on a different file than the one served.
/// <c>Cache-Control</c> is public for a day rather than <c>immutable</c>: a file can be overwritten or deleted, and a
/// day bounds how long a shared cache serves the old one.
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
        var isHead = HttpMethods.IsHead(Request.Method);

        DrivePublicReadResult result;
        try
        {
            result = await DrivePublicAppService.ReadAsync(containerName, blobName, input, CreateReadRequest(isHead));
        }
        catch (AbpValidationException)
        {
            // A size outside DrivePublicReadOptions.AllowedSizes, for GET and HEAD alike.
            return BadRequest();
        }
        catch (EntityNotFoundException)
        {
            // An address answers 404 itself: ABP's exception filter only shapes errors of object-returning (API) actions.
            return NotFound();
        }

        // Every header below comes from the one result: the same file's tag, type and bytes.
        var info = result.File;
        SetCacheHeaders(new EntityTagHeaderValue("\"" + info.EntityTag + "\""));
        if (result.NotModified)
        {
            result.Content?.Dispose();
            return StatusCode(StatusCodes.Status304NotModified);
        }

        SetContentDisposition(info.MimeType, FileName(info.Name));
        if (isHead)
        {
            // The headers without the bytes: nothing was read from storage, nothing was rendered.
            result.Content?.Dispose();
            Response.ContentType = info.MimeType;
            if ((input.Width ?? 0) <= 0 && (input.Height ?? 0) <= 0)
            {
                Response.ContentLength = info.Size;
            }

            return new EmptyResult();
        }

        if (result.Content == null)
        {
            // Not what the service promises for a GET that is not 304; no body is better than a wrong one.
            return NotFound();
        }

        return new FileStreamResult(result.Content.GetStream(), info.MimeType);
    }

    /// <summary>
    /// The client's entity tags from <c>If-None-Match</c>, as opaque values (no quotes, no <c>W/</c>: the comparison is
    /// weak), and whether the bytes are wanted. The service compares them with the tag of the file it finds.
    /// </summary>
    protected virtual DrivePublicReadRequest CreateReadRequest(bool headersOnly)
    {
        var request = new DrivePublicReadRequest { HeadersOnly = headersOnly };

        var ifNoneMatch = Request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch != null)
        {
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
        }

        return request;
    }

    protected virtual string FileName(string name)
    {
        var slash = name.LastIndexOf('/');
        return slash < 0 ? name : name.Substring(slash + 1);
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
