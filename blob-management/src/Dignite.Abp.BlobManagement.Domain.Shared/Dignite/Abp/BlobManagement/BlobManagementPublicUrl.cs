using System;
using System.Collections.Generic;
using System.Linq;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// Addresses of public reads: <c>/blobs/{containerName}/{blobName}</c>, optionally sized (<c>?Width=&amp;Height=</c>)
/// and naming the tenant (<c>?__tenant=</c>, which ABP's query-string tenant resolver reads - the request is
/// anonymous, so nothing else says whose file it is). One definition for the endpoint that serves them and for every
/// builder that hands them out. Not under <c>/api</c> and without the module's name: an address a page embeds and a
/// CDN caches, not an API - a gateway routes <c>/blobs/*</c> to Blob Management beside <c>/api/blob-management/*</c>.
/// </summary>
public static class BlobManagementPublicUrl
{
    public const string RoutePrefix = "blobs";

    /// <summary>The query parameter ABP's query-string tenant resolver reads (<c>TenantResolverConsts.DefaultTenantKey</c>).</summary>
    public const string TenantKey = "__tenant";

    /// <summary>The largest width or height a public read resizes to, and the largest picture it resizes from.</summary>
    public const int MaxDimension = 4096;

    /// <summary>
    /// The widths and heights a public read may be resized to when neither the container nor the host's default
    /// container names its own (<c>UseBlobManagementPublicSizes</c>). Every size is rendered on the request that asks
    /// for it, and each combination of width and height is a separate cache entry, so the list is kept short.
    /// </summary>
    public static readonly int[] DefaultSizes = { 64, 128, 256, 384, 512, 768, 1024, 1536, 2048, 4096 };

    /// <summary>
    /// The path and query of a public read, relative to Blob Management's address. The BLOB name may be a path: each of its
    /// segments is escaped, its slashes are kept. With both <paramref name="width"/> and <paramref name="height"/> a
    /// picture is cropped to fill the box; with one, the other follows its proportions. A width or height must be one
    /// the container allows (by default <see cref="DefaultSizes"/>; a host sets its own default and a container its own
    /// list with <c>UseBlobManagementPublicSizes</c>); any other size is answered 400. Not checked here: Blob Management
    /// may be a remote one whose lists this code cannot know - a container's list is in its DTO (<c>PublicSizes</c>).
    /// </summary>
    public static string Build(string containerName, string blobName, Guid? tenantId = null, int? width = null, int? height = null)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentException("A container name is required.", nameof(containerName));
        }

        if (string.IsNullOrWhiteSpace(blobName))
        {
            throw new ArgumentException("A BLOB name is required.", nameof(blobName));
        }

        var path = "/" + RoutePrefix + "/" + Uri.EscapeDataString(containerName) + "/" +
                   string.Join("/", blobName.Split('/').Select(Uri.EscapeDataString));

        var query = new List<string>();
        if (tenantId != null)
        {
            query.Add(TenantKey + "=" + tenantId.Value.ToString("D"));
        }

        if (width > 0)
        {
            query.Add("Width=" + width.Value);
        }

        if (height > 0)
        {
            query.Add("Height=" + height.Value);
        }

        return query.Count == 0 ? path : path + "?" + string.Join("&", query);
    }
}
