using System;
using System.Linq;
using Dignite.Abp.BlobManagement.Public;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The base of the addresses that serve a file's bytes to a browser: the public read and the read with a link. The
/// bytes go out through ASP.NET Core's <see cref="FileStreamResult"/>, which answers HTTP's conditional and range
/// requests - <c>If-None-Match</c>, <c>If-Modified-Since</c>, <c>If-Match</c>, <c>If-Unmodified-Since</c>,
/// <c>Range</c> and <c>If-Range</c> - from the read's <c>ETag</c> and <c>Last-Modified</c>, so a video, an audio file or
/// a long PDF can be read from where a player or a viewer seeks.
/// <para>
/// Two answers are given before the bytes are opened, since opening them is a request to the storage backend: a
/// <c>HEAD</c>, and a <c>304</c> to an <c>If-None-Match</c> that matches the tag of the file the service found
/// (<see cref="CreateReadRequest"/>).
/// </para>
/// <para>
/// A range is served where the stream can seek - the file system's, Azure's, a resized picture's, which is in memory.
/// From one that cannot (the S3 and MinIO providers'), the whole file is sent with <c>200</c>, as
/// <see cref="FileStreamResult"/> does; ABP's <c>IBlobContainer</c> has no ranged read to ask the backend for a part.
/// </para>
/// <para>
/// <b>Safety.</b> <c>X-Content-Type-Options: nosniff</c> always; a type outside <see cref="BlobManagementFileTypes"/> (the
/// curated list without SVG, HTML or scripts) is served as an attachment, never rendered on Blob Management's origin.
/// </para>
/// </summary>
public abstract class BlobManagementFileReadController : BlobManagementController
{
    /// <summary>
    /// The response to <paramref name="result"/>, once the caller has set its own caching headers: <c>304</c>, the
    /// headers of a <c>HEAD</c>, or the bytes. Every header comes from the one result: the same file's tag, date, type
    /// and bytes.
    /// </summary>
    /// <param name="headContentLength">The length a <c>HEAD</c> announces, where it is known without reading the
    /// bytes; <c>null</c> announces none.</param>
    protected virtual IActionResult CreateFileResult(BlobManagementPublicReadResult result, bool isHead, long? headContentLength)
    {
        var info = result.File;
        var entityTag = new EntityTagHeaderValue("\"" + info.EntityTag + "\"");
        var lastModified = GetLastModified(info);

        var headers = Response.GetTypedHeaders();
        headers.ETag = entityTag;
        headers.LastModified = lastModified;
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";

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
            if (headContentLength.HasValue)
            {
                Response.ContentLength = headContentLength.Value;
            }

            return new EmptyResult();
        }

        if (result.Content == null)
        {
            // Not what the service promises for a GET that is not 304; no body is better than a wrong one.
            return NotFound();
        }

        return new FileStreamResult(result.Content.GetStream(), info.MimeType)
        {
            EntityTag = entityTag,
            LastModified = lastModified,
            EnableRangeProcessing = true
        };
    }

    /// <summary>
    /// The client's entity tags from <c>If-None-Match</c>, as opaque values (no quotes, no <c>W/</c>: the comparison is
    /// weak), and whether the bytes are wanted. The service compares them with the tag of the file it finds.
    /// </summary>
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

    /// <summary>
    /// When the bytes were stored, for <c>Last-Modified</c>; <c>null</c> when the read does not say. A date without a
    /// kind is taken as local time, as ABP's clock writes it by default.
    /// </summary>
    protected virtual DateTimeOffset? GetLastModified(BlobManagementPublicFileDto info)
    {
        return info.LastModified == default ? null : new DateTimeOffset(info.LastModified);
    }

    protected virtual void SetContentDisposition(string mimeType, string? fileName)
    {
        var known = BlobManagementFileTypes.All.Any(type => type.MimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase));
        var disposition = new ContentDispositionHeaderValue(known ? "inline" : "attachment");
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            disposition.SetHttpFileName(fileName);
        }

        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
    }

    /// <summary>The last segment of a name that may be a path.</summary>
    protected virtual string FileName(string name)
    {
        var slash = name.LastIndexOf('/');
        return slash < 0 ? name : name.Substring(slash + 1);
    }
}
