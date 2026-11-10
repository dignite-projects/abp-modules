using System;
using System.Collections.Generic;
using System.Linq;

namespace Dignite.Drive;

/// <summary>
/// The file types the Drive knows, as one table of extension and MIME type, grouped as the container presets take
/// them. A preset allows the <b>MIME types</b> of its groups, because <c>Dignite.Abp.BlobStoring.Pipeline</c> checks
/// the type detected from the bytes; a file input's <c>accept</c> speaks <b>extensions</b>. Both come from this
/// table, so the two cannot drift apart.
/// <para>
/// Each MIME type is the one <c>MimeTypeDetector</c> reports for content of that extension, so an allowed
/// extension is never refused for being what it says it is. The Drive also names a physical BLOB with the
/// canonical extension of its detected type (<see cref="GetExtension"/>).
/// </para>
/// <para>
/// SVG, HTML and script types are deliberately absent: a container may be read publicly, and a file a browser
/// renders and executes would be stored XSS on the Drive's own origin. The exact raster types are listed rather than
/// <c>image/*</c>, which would admit <c>image/svg+xml</c>.
/// </para>
/// </summary>
public static class DriveFileTypes
{
    /// <summary>Raster pictures, the ones public reads resize on request.</summary>
    public static IReadOnlyList<DriveFileType> Images { get; } = new[]
    {
        new DriveFileType(".jpg", "image/jpeg"),
        new DriveFileType(".jpeg", "image/jpeg"),
        new DriveFileType(".png", "image/png"),
        new DriveFileType(".gif", "image/gif"),
        new DriveFileType(".webp", "image/webp")
    };

    /// <summary>Office documents, PDF and plain text.</summary>
    public static IReadOnlyList<DriveFileType> Documents { get; } = new[]
    {
        new DriveFileType(".pdf", "application/pdf"),
        new DriveFileType(".doc", "application/msword"),
        new DriveFileType(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
        new DriveFileType(".xls", "application/vnd.ms-excel"),
        new DriveFileType(".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        new DriveFileType(".ppt", "application/vnd.ms-powerpoint"),
        new DriveFileType(".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation"),
        new DriveFileType(".txt", "text/plain"),
        new DriveFileType(".csv", "text/csv")
    };

    /// <summary>
    /// What a web page links and the groups above do not hold: an icon and the web fonts, whose type the
    /// content-based <c>MimeTypeDetector</c> reads from the bytes.
    /// </summary>
    public static IReadOnlyList<DriveFileType> BinaryAssets { get; } = new[]
    {
        new DriveFileType(".ico", "image/x-icon"),
        new DriveFileType(".woff2", "font/woff2"),
        new DriveFileType(".woff", "font/woff"),
        new DriveFileType(".ttf", "font/ttf"),
        new DriveFileType(".otf", "font/otf")
    };

    public static IReadOnlyList<DriveFileType> Archives { get; } = new[]
    {
        new DriveFileType(".zip", "application/zip")
    };

    /// <summary>Every type of the table, <see cref="Images"/> first.</summary>
    public static IReadOnlyList<DriveFileType> All { get; } =
        Images.Concat(Documents).Concat(BinaryAssets).Concat(Archives).ToArray();

    /// <summary>The distinct MIME types of <paramref name="types"/>, in table order.</summary>
    public static string[] GetMimeTypes(IEnumerable<DriveFileType> types)
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

/// <summary>One row of <see cref="DriveFileTypes"/>: an extension (with its dot) and its MIME type.</summary>
public sealed class DriveFileType
{
    public DriveFileType(string extension, string mimeType)
    {
        Extension = extension;
        MimeType = mimeType;
    }

    public string Extension { get; }

    public string MimeType { get; }
}
