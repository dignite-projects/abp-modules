using System;
using System.Collections.Generic;
using System.Linq;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The file types Blob Management knows, as one table of extension and MIME type, grouped as the container presets take
/// them. A preset allows the <b>MIME types</b> of its groups, because <c>Dignite.Abp.BlobStoring.Pipeline</c> checks
/// the type detected from the bytes; a file input's <c>accept</c> speaks <b>extensions</b>. Both come from this
/// table, so the two cannot drift apart.
/// <para>
/// Each MIME type is the one <c>MimeTypeDetector</c> reports for content of that extension, so an allowed
/// extension is never refused for being what it says it is. Blob Management also names a physical BLOB with the
/// canonical extension of its detected type (<see cref="GetExtension"/>).
/// </para>
/// <para>
/// SVG, HTML and script types are deliberately absent: a container may be read publicly, and a file a browser
/// renders and executes would be stored XSS on Blob Management's own origin. The exact raster types are listed rather than
/// <c>image/*</c>, which would admit <c>image/svg+xml</c>.
/// </para>
/// </summary>
public static class BlobManagementFileTypes
{
    /// <summary>Raster pictures, the ones public reads resize on request.</summary>
    public static IReadOnlyList<BlobManagementFileType> Images { get; } = new[]
    {
        new BlobManagementFileType(".jpg", "image/jpeg"),
        new BlobManagementFileType(".jpeg", "image/jpeg"),
        new BlobManagementFileType(".png", "image/png"),
        new BlobManagementFileType(".gif", "image/gif"),
        new BlobManagementFileType(".webp", "image/webp")
    };

    /// <summary>Office documents, PDF and plain text.</summary>
    public static IReadOnlyList<BlobManagementFileType> Documents { get; } = new[]
    {
        new BlobManagementFileType(".pdf", "application/pdf"),
        new BlobManagementFileType(".doc", "application/msword"),
        new BlobManagementFileType(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
        new BlobManagementFileType(".xls", "application/vnd.ms-excel"),
        new BlobManagementFileType(".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        new BlobManagementFileType(".ppt", "application/vnd.ms-powerpoint"),
        new BlobManagementFileType(".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation"),
        new BlobManagementFileType(".txt", "text/plain"),
        new BlobManagementFileType(".csv", "text/csv")
    };

    /// <summary>
    /// What a web page links and the groups above do not hold: an icon and the web fonts, whose type the
    /// content-based <c>MimeTypeDetector</c> reads from the bytes.
    /// </summary>
    public static IReadOnlyList<BlobManagementFileType> BinaryAssets { get; } = new[]
    {
        new BlobManagementFileType(".ico", "image/x-icon"),
        new BlobManagementFileType(".woff2", "font/woff2"),
        new BlobManagementFileType(".woff", "font/woff"),
        new BlobManagementFileType(".ttf", "font/ttf"),
        new BlobManagementFileType(".otf", "font/otf")
    };

    public static IReadOnlyList<BlobManagementFileType> Archives { get; } = new[]
    {
        new BlobManagementFileType(".zip", "application/zip")
    };

    /// <summary>Every type of the table, <see cref="Images"/> first.</summary>
    public static IReadOnlyList<BlobManagementFileType> All { get; } =
        Images.Concat(Documents).Concat(BinaryAssets).Concat(Archives).ToArray();

    /// <summary>The distinct MIME types of <paramref name="types"/>, in table order.</summary>
    public static string[] GetMimeTypes(IEnumerable<BlobManagementFileType> types)
    {
        return types.Select(type => type.MimeType).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// The extensions of the table whose MIME type <paramref name="mimeTypes"/> allows - an exact type or a
    /// <c>type/*</c> wildcard, compared case-insensitively as the content-type check compares them.
    /// </summary>
    public static string[] GetExtensions(IEnumerable<string>? mimeTypes)
    {
        if (mimeTypes == null)
        {
            return Array.Empty<string>();
        }

        var allowed = mimeTypes.ToList();
        return All
            .Where(type => allowed.Any(mimeType => Matches(mimeType, type.MimeType)))
            .Select(type => type.Extension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// The canonical extension (with its dot) of <paramref name="mimeType"/>: the extension of the <b>first</b> row
    /// of <see cref="All"/> with that MIME type (<c>image/jpeg</c> is <c>.jpg</c>, not <c>.jpeg</c>). <c>null</c>
    /// when the table has no row for the type.
    /// </summary>
    public static string? GetExtension(string? mimeType)
    {
        return mimeType == null
            ? null
            : All.FirstOrDefault(type => type.MimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase))?.Extension;
    }

    /// <summary>Whether <paramref name="mimeType"/> is one of the raster <see cref="Images"/> types.</summary>
    public static bool IsImage(string? mimeType)
    {
        return mimeType != null && Images.Any(type => type.MimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase));
    }

    private static bool Matches(string allowed, string mimeType)
    {
        return allowed.EndsWith("/*", StringComparison.Ordinal)
            ? mimeType.StartsWith(allowed.Substring(0, allowed.Length - 1), StringComparison.OrdinalIgnoreCase)
            : allowed.Equals(mimeType, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>One row of <see cref="BlobManagementFileTypes"/>: an extension (with its dot) and its MIME type.</summary>
public sealed class BlobManagementFileType
{
    public BlobManagementFileType(string extension, string mimeType)
    {
        Extension = extension;
        MimeType = mimeType;
    }

    public string Extension { get; }

    public string MimeType { get; }
}
