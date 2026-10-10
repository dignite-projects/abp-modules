using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.FileStoring;

/// <summary>
/// The default <see cref="IMimeTypeDetector"/>: a signature ("magic number") probe over the first
/// bytes of the content, reconciled with the file extension.
/// <list type="bullet">
/// <item>Content with a known signature decides the type. If the extension names a type of a
/// different kind (an executable named <c>.png</c>, a ZIP named <c>.pdf</c>, a PNG named <c>.txt</c>),
/// the upload is rejected. Within one kind the content wins (a PNG named <c>.jpg</c> is
/// <c>image/png</c>), except for generic containers, where the extension says which format the
/// container holds (a ZIP named <c>.docx</c> is the Word MIME type).</item>
/// <item>Content without a known signature (plain text, CSV, JSON, SVG, ...) falls back to the
/// extension — those formats have no signature to check. An extension whose format always has a
/// signature (images, PDF, Office, archives, executables) is rejected when the signature is missing:
/// that is a disguised file.</item>
/// <item>Neither known: <c>application/octet-stream</c>.</item>
/// </list>
/// The image signatures cover every type in <see cref="ImageFormatHelper.AllowedImageUploadFormats"/>.
/// </summary>
public class MimeTypeDetector : IMimeTypeDetector, ITransientDependency
{
    public const string DefaultMimeType = "application/octet-stream";

    /// <summary>
    /// How many leading bytes the signature probe reads.
    /// </summary>
    protected const int HeaderLength = 64;

    private const string RasterImageKind = "raster-image";

    private static readonly Dictionary<string, (string MimeType, bool RequiresSignature)> Extensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Images
            [".jpg"] = ("image/jpeg", true),
            [".jpeg"] = ("image/jpeg", true),
            [".jfif"] = ("image/jpeg", true),
            [".png"] = ("image/png", true),
            [".gif"] = ("image/gif", true),
            [".bmp"] = ("image/bmp", true),
            [".webp"] = ("image/webp", true),
            [".tif"] = ("image/tiff", true),
            [".tiff"] = ("image/tiff", true),
            [".ico"] = ("image/x-icon", true),
            [".svg"] = ("image/svg+xml", false),
            [".avif"] = ("image/avif", false),
            [".heic"] = ("image/heic", false),

            // Documents
            [".pdf"] = ("application/pdf", true),
            [".rtf"] = ("application/rtf", true),
            [".doc"] = ("application/msword", true),
            [".xls"] = ("application/vnd.ms-excel", true),
            [".ppt"] = ("application/vnd.ms-powerpoint", true),
            [".msg"] = ("application/vnd.ms-outlook", true),
            [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", true),
            [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", true),
            [".pptx"] = ("application/vnd.openxmlformats-officedocument.presentationml.presentation", true),
            [".odt"] = ("application/vnd.oasis.opendocument.text", true),
            [".ods"] = ("application/vnd.oasis.opendocument.spreadsheet", true),
            [".odp"] = ("application/vnd.oasis.opendocument.presentation", true),
            [".epub"] = ("application/epub+zip", true),
            [".txt"] = ("text/plain", false),
            [".log"] = ("text/plain", false),
            [".csv"] = ("text/csv", false),
            [".md"] = ("text/markdown", false),
            [".json"] = ("application/json", false),
            [".xml"] = ("application/xml", false),
            [".htm"] = ("text/html", false),
            [".html"] = ("text/html", false),

            // Archives
            [".zip"] = ("application/zip", true),
            [".gz"] = ("application/gzip", true),
            [".7z"] = ("application/x-7z-compressed", true),
            [".rar"] = ("application/vnd.rar", true),
            [".tar"] = ("application/x-tar", false),

            // Audio and video. Containers whose signature is not guaranteed at offset 0 (MP3 without
            // an ID3 tag, MP4 with a leading box other than 'ftyp') are not required to carry one.
            [".mp3"] = ("audio/mpeg", false),
            [".wav"] = ("audio/wav", true),
            [".flac"] = ("audio/flac", false),
            [".ogg"] = ("audio/ogg", false),
            [".oga"] = ("audio/ogg", false),
            [".ogv"] = ("video/ogg", false),
            [".m4a"] = ("audio/mp4", false),
            [".mp4"] = ("video/mp4", false),
            [".m4v"] = ("video/mp4", false),
            [".mov"] = ("video/quicktime", false),
            [".webm"] = ("video/webm", false),
            [".mkv"] = ("video/x-matroska", false),
            [".avi"] = ("video/x-msvideo", true),

            // Executables
            [".exe"] = ("application/x-msdownload", true),
            [".dll"] = ("application/x-msdownload", true),
        };

    public virtual async Task<string> DetectAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(stream, nameof(stream));
        Check.NotNull(fileName, nameof(fileName));

        if (!stream.CanSeek)
        {
            throw new ArgumentException("The stream must be seekable so its content can be probed.", nameof(stream));
        }

        var header = new byte[HeaderLength];
        stream.Position = 0;
        var length = await stream.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, cancellationToken);
        stream.Position = 0;

        var fromContent = DetectFromContent(header.AsSpan(0, length));
        var extension = Path.GetExtension(fileName);
        var fromExtension = GetMimeTypeFromExtension(extension);

        if (fromContent != null)
        {
            if (fromExtension == null)
            {
                return fromContent;
            }

            if (GetKind(fromContent) != GetKind(fromExtension.Value.MimeType))
            {
                throw CreateMismatchException(extension);
            }

            return IsGenericContainer(fromContent) ? fromExtension.Value.MimeType : fromContent;
        }

        if (fromExtension == null)
        {
            return DefaultMimeType;
        }

        if (fromExtension.Value.RequiresSignature)
        {
            throw CreateMismatchException(extension);
        }

        return fromExtension.Value.MimeType;
    }

    /// <summary>
    /// The extension's MIME type and whether that format always starts with a signature this
    /// detector knows. <c>null</c> for an unknown or missing extension.
    /// </summary>
    protected virtual (string MimeType, bool RequiresSignature)? GetMimeTypeFromExtension(string extension)
    {
        return !string.IsNullOrEmpty(extension) && Extensions.TryGetValue(extension, out var entry)
            ? entry
            : null;
    }

    /// <summary>
    /// The MIME type identified by the leading bytes, or <c>null</c> when no known signature matches.
    /// </summary>
    protected virtual string? DetectFromContent(ReadOnlySpan<byte> header)
    {
        if (StartsWith(header, 0xFF, 0xD8, 0xFF))
        {
            return "image/jpeg";
        }

        if (StartsWith(header, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A))
        {
            return "image/png";
        }

        if (StartsWith(header, "GIF87a"u8) || StartsWith(header, "GIF89a"u8))
        {
            return "image/gif";
        }

        if (IsBmp(header))
        {
            return "image/bmp";
        }

        if (header.Length >= 12 && StartsWith(header, "RIFF"u8))
        {
            var form = header.Slice(8, 4);
            if (form.SequenceEqual("WEBP"u8))
            {
                return "image/webp";
            }

            if (form.SequenceEqual("WAVE"u8))
            {
                return "audio/wav";
            }

            if (form.SequenceEqual("AVI "u8))
            {
                return "video/x-msvideo";
            }
        }

        if (StartsWith(header, 0x49, 0x49, 0x2A, 0x00) || StartsWith(header, 0x4D, 0x4D, 0x00, 0x2A))
        {
            return "image/tiff";
        }

        if (header.Length >= 6 && StartsWith(header, 0x00, 0x00, 0x01, 0x00) &&
            BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(4, 2)) > 0)
        {
            return "image/x-icon";
        }

        if (StartsWith(header, "%PDF-"u8))
        {
            return "application/pdf";
        }

        if (StartsWith(header, 0x50, 0x4B, 0x03, 0x04) ||
            StartsWith(header, 0x50, 0x4B, 0x05, 0x06) ||
            StartsWith(header, 0x50, 0x4B, 0x07, 0x08))
        {
            return "application/zip";
        }

        if (StartsWith(header, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1))
        {
            return "application/x-cfb";
        }

        if (StartsWith(header, 0x1F, 0x8B))
        {
            return "application/gzip";
        }

        if (StartsWith(header, 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C))
        {
            return "application/x-7z-compressed";
        }

        if (StartsWith(header, 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07))
        {
            return "application/vnd.rar";
        }

        if (StartsWith(header, "{\\rtf"u8))
        {
            return "application/rtf";
        }

        if (header.Length >= 12 && header.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return DetectIsoBaseMediaFile(header.Slice(8, 4));
        }

        if (header.Length >= 5 && StartsWith(header, "ID3"u8) && header[3] is >= 2 and <= 4 && header[4] != 0xFF)
        {
            return "audio/mpeg";
        }

        if (header.Length >= 2 && header[0] == 0xFF && header[1] is 0xFB or 0xF3 or 0xF2)
        {
            return "audio/mpeg";
        }

        if (StartsWith(header, "OggS"u8))
        {
            return "application/ogg";
        }

        if (StartsWith(header, "fLaC"u8))
        {
            return "audio/flac";
        }

        if (StartsWith(header, 0x1A, 0x45, 0xDF, 0xA3))
        {
            return "video/webm";
        }

        if (IsPortableExecutable(header))
        {
            return "application/x-msdownload";
        }

        if (StartsWith(header, 0x7F, 0x45, 0x4C, 0x46))
        {
            return "application/x-elf";
        }

        if (StartsWith(header, 0xFE, 0xED, 0xFA, 0xCE) || StartsWith(header, 0xFE, 0xED, 0xFA, 0xCF) ||
            StartsWith(header, 0xCE, 0xFA, 0xED, 0xFE) || StartsWith(header, 0xCF, 0xFA, 0xED, 0xFE))
        {
            return "application/x-mach-binary";
        }

        if (StartsWith(header, 0x00, 0x61, 0x73, 0x6D))
        {
            return "application/wasm";
        }

        return null;
    }

    /// <summary>
    /// Types that only say "this is a ZIP / compound file / Ogg / Matroska / ISO media container";
    /// within the same kind, the extension names the actual format.
    /// </summary>
    protected virtual bool IsGenericContainer(string mimeType)
    {
        return mimeType is "application/zip" or "application/x-cfb" or "application/ogg" or "video/webm" or "video/mp4";
    }

    /// <summary>
    /// Groups MIME types that share one signature, so content and extension are compared by kind.
    /// </summary>
    protected virtual string GetKind(string mimeType)
    {
        return mimeType switch
        {
            "image/jpeg" or "image/png" or "image/gif" or "image/bmp" or "image/webp" or "image/tiff" or "image/x-icon"
                => RasterImageKind,
            "application/zip" or "application/epub+zip" or
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" or
                "application/vnd.oasis.opendocument.text" or
                "application/vnd.oasis.opendocument.spreadsheet" or
                "application/vnd.oasis.opendocument.presentation"
                => "zip",
            "application/x-cfb" or "application/msword" or "application/vnd.ms-excel" or
                "application/vnd.ms-powerpoint" or "application/vnd.ms-outlook"
                => "compound-file",
            "application/ogg" or "audio/ogg" or "video/ogg" => "ogg",
            "video/webm" or "audio/webm" or "video/x-matroska" => "matroska",
            "video/mp4" or "audio/mp4" or "video/quicktime" or "image/avif" or "image/heic" => "iso-media",
            "application/x-msdownload" or "application/x-elf" or "application/x-mach-binary" => "executable",
            _ => mimeType
        };
    }

    protected virtual Exception CreateMismatchException(string extension)
    {
        return new BusinessException(
                code: FileErrorCodes.Files.ContentTypeMismatch,
                message: $"The file content does not match its extension '{extension}'.")
            .WithData("Extension", extension);
    }

    private static string DetectIsoBaseMediaFile(ReadOnlySpan<byte> brand)
    {
        if (brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8))
        {
            return "image/avif";
        }

        if (brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8) ||
            brand.SequenceEqual("mif1"u8) || brand.SequenceEqual("msf1"u8))
        {
            return "image/heic";
        }

        if (brand.SequenceEqual("M4A "u8) || brand.SequenceEqual("M4B "u8))
        {
            return "audio/mp4";
        }

        if (brand.SequenceEqual("qt  "u8))
        {
            return "video/quicktime";
        }

        return "video/mp4";
    }

    private static bool IsBmp(ReadOnlySpan<byte> header)
    {
        // "BM" alone would also match text; require a known DIB header size as well.
        if (header.Length < 18 || !StartsWith(header, "BM"u8))
        {
            return false;
        }

        var dibHeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(14, 4));
        return dibHeaderSize is 12 or 40 or 52 or 56 or 64 or 108 or 124;
    }

    private static bool IsPortableExecutable(ReadOnlySpan<byte> header)
    {
        // "MZ" alone would also match text; require a plausible e_lfanew (offset of the PE header).
        if (header.Length < 64 || !StartsWith(header, "MZ"u8))
        {
            return false;
        }

        var peHeaderOffset = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(0x3C, 4));
        return peHeaderOffset is >= 0x40 and < 0x1000;
    }

    private static bool StartsWith(ReadOnlySpan<byte> header, params ReadOnlySpan<byte> signature)
    {
        return header.StartsWith(signature);
    }
}
