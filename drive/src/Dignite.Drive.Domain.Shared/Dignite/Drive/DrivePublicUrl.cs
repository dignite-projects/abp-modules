using System;
using System.Collections.Generic;
using System.Linq;

namespace Dignite.Drive;

/// <summary>
/// Addresses of public reads: <c>/api/drive/public/{containerName}/{blobName}</c>, optionally sized
/// (<c>?Width=&amp;Height=</c>) and naming the tenant (<c>?__tenant=</c>, which ABP's query-string tenant resolver
/// reads - the request is anonymous, so nothing else says whose file it is). One definition for the endpoint that
/// serves them and for every builder that hands them out.
/// </summary>
public static class DrivePublicUrl
{
    public const string RoutePrefix = "api/drive/public";

    /// <summary>The query parameter ABP's query-string tenant resolver reads (<c>TenantResolverConsts.DefaultTenantKey</c>).</summary>
    public const string TenantKey = "__tenant";

    /// <summary>The largest width or height a public read resizes to, and the largest picture it resizes from.</summary>
    public const int MaxDimension = 4096;

    /// <summary>
    /// The path and query of a public read, relative to the Drive's address. The BLOB name may be a path: each of its
    /// segments is escaped, its slashes are kept. With both <paramref name="width"/> and <paramref name="height"/> a
    /// picture is cropped to fill the box; with one, the other follows its proportions.
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
