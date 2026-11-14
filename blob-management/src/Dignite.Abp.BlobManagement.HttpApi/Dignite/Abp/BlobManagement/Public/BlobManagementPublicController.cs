using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Validation;

namespace Dignite.Abp.BlobManagement.Public;

/// <summary>
/// Public reads: <c>GET|HEAD /blobs/{containerName}/{blobName}</c>, optionally <c>?Width=&amp;Height=</c>, and
/// <c>?__tenant=</c> to name the tenant of an anonymous request (see <see cref="BlobManagementPublicUrl"/>). An address, not an
/// API: it is left out of ABP's API definition, so no client proxy is generated for it. Conditional and range requests
/// and the safety headers are <see cref="BlobManagementFileReadController"/>'s.
/// <para>
/// <b>Caching.</b> The <c>ETag</c> is the read's <see cref="BlobManagementPublicFileDto.EntityTag"/> - the file's
/// <see cref="BlobManagementPublicFileDto.ContentId"/> plus the size asked; it changes when the file is overwritten. A
/// response is answered from one lookup of the file (<see cref="IBlobManagementPublicAppService.ReadAsync"/>): a file
/// addressed by name can be overwritten between two lookups, and a response made from both would store the new bytes
/// in a shared cache under the old file's <c>ETag</c>, and decide inline or attachment on a different file than the one
/// served. <c>Cache-Control</c> is public for a day rather than <c>immutable</c>: a file can be overwritten or deleted,
/// and a day bounds how long a shared cache serves the old one. No cookie is set on a response meant for a shared
/// cache.
/// </para>
/// </summary>
[Area(BlobManagementRemoteServiceConsts.ModuleName)]
[ApiExplorerSettings(IgnoreApi = true)]
[Route(BlobManagementPublicUrl.RoutePrefix)]
public class BlobManagementPublicController : BlobManagementFileReadController
{
    public const int MaxAgeSeconds = 24 * 60 * 60;

    protected IBlobManagementPublicAppService BlobManagementPublicAppService { get; }

    public BlobManagementPublicController(IBlobManagementPublicAppService drivePublicAppService)
    {
        BlobManagementPublicAppService = drivePublicAppService;
    }

    [HttpGet]
    [HttpHead]
    [Route("{containerName}/{*blobName}")]
    public virtual async Task<IActionResult> GetAsync(string containerName, string blobName, [FromQuery] BlobManagementPublicReadInput input)
    {
        var isHead = HttpMethods.IsHead(Request.Method);

        BlobManagementPublicReadResult result;
        try
        {
            result = await BlobManagementPublicAppService.ReadAsync(containerName, blobName, input, CreateReadRequest(isHead));
        }
        catch (AbpValidationException)
        {
            // A size outside the container's public sizes, for GET and HEAD alike.
            return BadRequest();
        }
        catch (EntityNotFoundException)
        {
            // An address answers 404 itself: ABP's exception filter only shapes errors of object-returning (API) actions.
            return NotFound();
        }

        SetCacheHeaders();

        // A HEAD announces the stored length; a size asked is rendered, and its length is not known without rendering.
        var sized = (input.Width ?? 0) > 0 || (input.Height ?? 0) > 0;
        return CreateFileResult(result, isHead, sized ? null : result.File.Size);
    }

    protected virtual void SetCacheHeaders()
    {
        Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = TimeSpan.FromSeconds(MaxAgeSeconds)
        };

        // A cache may refuse a response with Set-Cookie, or store it and hand the cookie to every visitor after. The
        // host's tenant and localization middleware append theirs before this action runs.
        Response.Headers.Remove(HeaderNames.SetCookie);
    }
}
