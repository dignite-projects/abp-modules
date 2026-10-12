using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileSignatures;
using FileSignatures.Formats;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// The default <see cref="IMimeTypeDetector"/>: a signature ("magic number") probe, a text sniff for content
/// without a signature, and a policy that reconciles the result with the file extension.
/// <list type="bullet">
/// <item>Signatures come from FileSignatures (<see cref="IFileFormatInspector"/>), which also looks inside
/// archives, so an Office Open XML or OpenDocument file is told apart from a plain ZIP. Signatures short
/// enough to occur in text (<c>BM</c>, <c>MZ</c>, <c>ID3</c>, ...) must be confirmed by the rest of the
/// header (<see cref="IsSignatureConfirmed"/>). Formats FileSignatures does not know (WAV, AVIF/HEIC, other
/// ISO media brands, MP3 frames without an ID3 tag, generic compound files, Mach-O, empty ZIPs) fall back to
/// <see cref="DetectFromContent"/>.</item>
/// <item>Content with a known signature decides the type. If the extension names a type of a different kind
/// (an executable named <c>.png</c>, a ZIP named <c>.pdf</c>, a PNG named <c>.txt</c>), it is rejected.
/// Within one kind the content wins (a PNG named <c>.jpg</c> is <c>image/png</c>), except for generic
/// containers, where the extension says which format the container holds (a ZIP named <c>.epub</c> is
/// <c>application/epub+zip</c>).</item>
/// <item>Content without a signature is sniffed for text (<see cref="IsText"/>): XML, HTML and SVG are
/// recognised from their markup (<see cref="DetectTextFormat"/>), other text takes the extension's text type
/// or <c>text/plain</c>. All text types are one kind, so HTML named <c>.txt</c> is <c>text/html</c>; text named
/// with a binary extension (<c>.png</c>, <c>.mp3</c>) is rejected.</item>
/// <item>Binary content without a signature takes the extension's type when that format has no guaranteed
/// signature (MP4, TAR, ...), is rejected when it should have one, and is <see cref="DefaultMimeType"/>
/// otherwise.</item>
/// </list>
/// </summary>
public class MimeTypeDetector : IMimeTypeDetector, ITransientDependency
{
    /// <summary>
    /// The type of content that cannot be identified.
    /// </summary>
    public const string DefaultMimeType = "application/octet-stream";

    /// <summary>
    /// How many leading bytes are read for the header probe and the text sniff (the WHATWG MIME Sniffing
    /// resource header size).
    /// </summary>
    protected const int SampleLength = 1445;

    /// <summary>
    /// The kind shared by every text type (see <see cref="GetKind"/>).
    /// </summary>
    protected const string TextKind = "text";

    /// <summary>
    /// <c>[Content_Types].xml</c> entries larger than this are not inspected (FileSignatures parses that entry
    /// to tell Office formats apart, and a hostile archive could declare a huge one); such an archive is
    /// classified from its header as a plain ZIP.
    /// </summary>
    protected const long MaxInspectedZipEntryLength = 1024 * 1024;

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

    // Root elements that make markup HTML. The generic short tags of the WHATWG list (<p>, <a>, <b>, <div>,
    // ...) are left out: Markdown and plain text often start with them.
    private static readonly HashSet<string> HtmlRootElements =
        new(StringComparer.OrdinalIgnoreCase) { "html", "head", "body", "script", "iframe", "style", "title" };

    protected IFileFormatInspector FileFormatInspector { get; }

    public MimeTypeDetector(IFileFormatInspector fileFormatInspector)
    {
        FileFormatInspector = fileFormatInspector;
    }

    public virtual async Task<string> DetectAsync(
        Stream stream,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(stream, nameof(stream));

        if (!stream.CanSeek)
        {
            throw new ArgumentException("The stream must be seekable so its content can be probed.", nameof(stream));
        }

        var sample = new byte[SampleLength];
        int length;
        stream.Position = 0;
        try
        {
            length = await stream.ReadAtLeastAsync(sample, SampleLength, throwOnEndOfStream: false, cancellationToken);
        }
        finally
        {
            stream.Position = 0;
        }

        cancellationToken.ThrowIfCancellationRequested();

        string? fromSignature;
        try
        {
            fromSignature = length == 0 ? null : DetectFromSignature(stream, sample.AsSpan(0, length));
        }
        finally
        {
            stream.Position = 0;
        }

        var extension = string.IsNullOrEmpty(fileName) ? string.Empty : Path.GetExtension(fileName);
        var fromExtension = GetMimeTypeFromExtension(extension);

        if (fromSignature != null)
        {
            if (fromExtension == null)
            {
                return fromSignature;
            }

            if (GetKind(fromSignature) != GetKind(fromExtension.Value.MimeType))
            {
                throw CreateMismatchException(extension);
            }

            return IsGenericContainer(fromSignature) ? fromExtension.Value.MimeType : fromSignature;
        }

        if (length == 0)
        {
            // Nothing to sniff: an empty file is what its extension says, unless that format has a signature.
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

        if (IsText(sample.AsSpan(0, length)))
        {
            var fromText = DetectTextFormat(sample.AsSpan(0, length));
            if (fromExtension == null)
            {
                return fromText ?? "text/plain";
            }

            if (GetKind(fromExtension.Value.MimeType) != TextKind)
            {
                throw CreateMismatchException(extension);
            }

            return fromText ?? fromExtension.Value.MimeType;
        }

        if (fromExtension == null)
        {
            return DefaultMimeType;
        }

        if (fromExtension.Value.RequiresSignature)
        {
            throw CreateMismatchException(extension);
        }

        // Binary content claiming to be text is not text; formats without a guaranteed signature keep their type.
        return GetKind(fromExtension.Value.MimeType) == TextKind ? DefaultMimeType : fromExtension.Value.MimeType;
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
    /// The MIME type identified by a signature, or <c>null</c> when none matches: FileSignatures first
    /// (confirmed by <see cref="IsSignatureConfirmed"/> and named by <see cref="NormalizeMediaType"/>), then
    /// <see cref="DetectFromContent"/>. <paramref name="stream"/> is at position 0 and may be read freely;
    /// <paramref name="header"/> holds its first bytes.
    /// </summary>
    protected virtual string? DetectFromSignature(Stream stream, ReadOnlySpan<byte> header)
    {
        FileFormat? format = null;
        if (CanInspect(stream, header))
        {
            try
            {
                format = FileFormatInspector.DetermineFileFormat(stream);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
            {
                // The inspector parses archives and compound files; a malformed (or hostile) one must not turn
                // into a server error. The header probe below still classifies it.
                format = null;
            }
            finally
            {
                stream.Position = 0;
            }
        }

        if (format != null && IsSignatureConfirmed(format, header))
        {
            return NormalizeMediaType(format);
        }

        return DetectFromContent(header);
    }

    /// <summary>
    /// Whether <see cref="FileFormatInspector"/> may inspect the stream. By default a ZIP whose
    /// <c>[Content_Types].xml</c> entry is larger than <see cref="MaxInspectedZipEntryLength"/> is not inspected.
    /// </summary>
    protected virtual bool CanInspect(Stream stream, ReadOnlySpan<byte> header)
    {
        if (!StartsWith(header, 0x50, 0x4B, 0x03, 0x04))
        {
            return true;
        }

        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var contentTypes = archive.GetEntry("[Content_Types].xml");
            return contentTypes == null || contentTypes.Length <= MaxInspectedZipEntryLength;
        }
        catch (InvalidDataException)
        {
            // Not a readable archive: the inspector will not open it either.
            return true;
        }
        finally
        {
            stream.Position = 0;
        }
    }

    /// <summary>
    /// Whether a FileSignatures match is trusted. Signatures of two to four printable bytes also occur at the
    /// start of text, so they need the rest of the header to agree.
    /// </summary>
    protected virtual bool IsSignatureConfirmed(FileFormat format, ReadOnlySpan<byte> header)
    {
        return format switch
        {
            Bmp => IsBmp(header),
            Executable => IsPortableExecutable(header),
            Mpeg3 => IsId3Tag(header),
            Webp or Avi => header.StartsWith("RIFF"u8),
            Aiff => header.StartsWith("FORM"u8),
            Flash => header.Length >= 4 && header[3] == 0x01,
            Midi => header.Length >= 8 && header.Slice(4, 4).SequenceEqual(new byte[] { 0x00, 0x00, 0x00, 0x06 }),
            _ => true
        };
    }

    /// <summary>
    /// The MIME type for a FileSignatures format, with FileSignatures' names mapped to the ones this detector
    /// uses (and a missing <c>application/</c> prefix fixed).
    /// </summary>
    protected virtual string NormalizeMediaType(FileFormat format)
    {
        return format.MediaType switch
        {
            "application/vnd.microsoft.portable-executable" => "application/x-msdownload",
            "image/vnd.microsoft.icon" => "image/x-icon",
            "video/matroska" => "video/x-matroska",
            // Ogg is a container; the extension says whether it holds audio or video.
            "audio/ogg" => "application/ogg",
            "vnd.ms-excel.sheet.binary.macroEnabled.12" => "application/vnd.ms-excel.sheet.binary.macroEnabled.12",
            var mediaType => mediaType
        };
    }

    /// <summary>
    /// The header probe for formats FileSignatures does not identify (and the fallback when it fails):
    /// the MIME type identified by the leading bytes, or <c>null</c> when no known signature matches.
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

        if (IsId3Tag(header))
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
    /// Whether the sample is text: a UTF-16 byte order mark, or no byte the WHATWG MIME Sniffing standard calls a
    /// binary data byte (control characters other than tab, line feed, form feed, carriage return and escape).
    /// UTF-8 validity is deliberately not required, so text in legacy encodings (GBK, Shift_JIS, Latin-1 CSV
    /// exports) is still text.
    /// </summary>
    protected virtual bool IsText(ReadOnlySpan<byte> sample)
    {
        if (StartsWith(sample, 0xFF, 0xFE) || StartsWith(sample, 0xFE, 0xFF))
        {
            return true;
        }

        foreach (var b in sample)
        {
            if (b <= 0x08 || b == 0x0B || b is >= 0x0E and <= 0x1A || b is >= 0x1C and <= 0x1F)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The markup type of a text sample, or <c>null</c> for other text: <c>image/svg+xml</c> when the root
    /// element is <c>svg</c>; <c>text/html</c> for an HTML doctype or an <c>html</c>/<c>head</c>/<c>body</c>/
    /// <c>script</c>/<c>iframe</c>/<c>style</c>/<c>title</c> root; <c>application/xml</c> for any other
    /// document with an XML declaration. Leading whitespace, a byte order mark, comments, processing
    /// instructions and a doctype are skipped while looking for the root element.
    /// </summary>
    protected virtual string? DetectTextFormat(ReadOnlySpan<byte> sample)
    {
        var text = DecodeSample(sample).TrimStart('﻿', ' ', '\t', '\r', '\n', '\f');
        if (!text.StartsWith('<'))
        {
            return null;
        }

        if (StartsWithTag(text, "<!DOCTYPE html"))
        {
            return "text/html";
        }

        if (StartsWithTag(text, "<!DOCTYPE svg"))
        {
            return "image/svg+xml";
        }

        var root = FindRootElementName(text);
        if (root != null)
        {
            var localName = root[(root.IndexOf(':') + 1)..];
            if (localName.Equals("svg", StringComparison.OrdinalIgnoreCase))
            {
                return "image/svg+xml";
            }

            if (HtmlRootElements.Contains(localName))
            {
                return "text/html";
            }
        }

        return text.StartsWith("<?xml", StringComparison.Ordinal) ? "application/xml" : null;
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
    /// Groups MIME types that share one signature (or, for text, none), so content and extension are
    /// compared by kind.
    /// </summary>
    protected virtual string GetKind(string mimeType)
    {
        return mimeType switch
        {
            "image/jpeg" or "image/png" or "image/gif" or "image/bmp" or "image/webp" or "image/tiff" or "image/x-icon"
                => RasterImageKind,
            "application/zip" or "application/epub+zip" or
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
                "application/vnd.openxmlformats-officedocument.wordprocessingml.template" or
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" or
                "application/vnd.ms-word.document.macroEnabled.12" or
                "application/vnd.ms-excel.sheet.macroEnabled.12" or
                "application/vnd.ms-excel.sheet.binary.macroEnabled.12" or
                "application/vnd.ms-powerpoint.presentation.macroEnabled.12" or
                "application/vnd.ms-xpsdocument" or
                "application/vnd.oasis.opendocument.text" or
                "application/vnd.oasis.opendocument.spreadsheet" or
                "application/vnd.oasis.opendocument.presentation"
                => "zip",
            "application/x-cfb" or "application/msword" or "application/vnd.ms-excel" or
                "application/vnd.ms-powerpoint" or "application/vnd.ms-outlook"
                => "compound-file",
            "application/ogg" or "audio/ogg" or "video/ogg" => "ogg",
            "video/webm" or "audio/webm" or "video/x-matroska" => "matroska",
            "video/mp4" or "audio/mp4" or "video/quicktime" or "video/3gpp" or "image/avif" or "image/heic" => "iso-media",
            "application/x-msdownload" or "application/x-elf" or "application/x-mach-binary" => "executable",
            "application/json" or "application/xml" or "image/svg+xml" => TextKind,
            _ when mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) => TextKind,
            _ => mimeType
        };
    }

    protected virtual Exception CreateMismatchException(string extension)
    {
        return new BusinessException(
                code: BlobStoringPipelineErrorCodes.ContentTypeMismatch,
                message: $"The content does not match its extension '{extension}'.")
            .WithData("Extension", extension);
    }

    private static string DecodeSample(ReadOnlySpan<byte> sample)
    {
        if (StartsWith(sample, 0xFF, 0xFE))
        {
            return Encoding.Unicode.GetString(sample[2..]);
        }

        if (StartsWith(sample, 0xFE, 0xFF))
        {
            return Encoding.BigEndianUnicode.GetString(sample[2..]);
        }

        // Markup is matched on ASCII only, which every ASCII-compatible encoding shares.
        return Encoding.Latin1.GetString(StartsWith(sample, 0xEF, 0xBB, 0xBF) ? sample[3..] : sample);
    }

    private static bool StartsWithTag(string text, string tag)
    {
        return text.StartsWith(tag, StringComparison.OrdinalIgnoreCase) &&
               (text.Length == tag.Length || IsTagTerminator(text[tag.Length]));
    }

    private static bool IsTagTerminator(char c)
    {
        return c is ' ' or '\t' or '\r' or '\n' or '\f' or '>' or '/';
    }

    /// <summary>
    /// The name of the first element, skipping processing instructions, comments and doctypes; <c>null</c>
    /// when text other than whitespace comes first or the sample ends before an element starts.
    /// </summary>
    private static string? FindRootElementName(string text)
    {
        var index = 0;
        while (true)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index >= text.Length || text[index] != '<')
            {
                return null;
            }

            if (string.CompareOrdinal(text, index, "<?", 0, 2) == 0)
            {
                index = SkipPast(text, index + 2, "?>");
            }
            else if (string.CompareOrdinal(text, index, "<!--", 0, 4) == 0)
            {
                index = SkipPast(text, index + 4, "-->");
            }
            else if (string.CompareOrdinal(text, index, "<!", 0, 2) == 0)
            {
                index = SkipDeclaration(text, index + 2);
            }
            else
            {
                var start = index + 1;
                var end = start;
                while (end < text.Length && !IsTagTerminator(text[end]))
                {
                    end++;
                }

                return end > start ? text[start..end] : null;
            }

            if (index < 0)
            {
                return null;
            }
        }
    }

    private static int SkipPast(string text, int start, string terminator)
    {
        var end = text.IndexOf(terminator, start, StringComparison.Ordinal);
        return end < 0 ? -1 : end + terminator.Length;
    }

    private static int SkipDeclaration(string text, int start)
    {
        // <!DOCTYPE ... [ internal subset ] >
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '[':
                    depth++;
                    break;
                case ']':
                    depth--;
                    break;
                case '>' when depth <= 0:
                    return i + 1;
            }
        }

        return -1;
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

    private static bool IsId3Tag(ReadOnlySpan<byte> header)
    {
        // "ID3" alone would also match text; require a valid major version and revision byte.
        return header.Length >= 5 && StartsWith(header, "ID3"u8) && header[3] is >= 2 and <= 4 && header[4] != 0xFF;
    }

    private static bool StartsWith(ReadOnlySpan<byte> header, params ReadOnlySpan<byte> signature)
    {
        return header.StartsWith(signature);
    }
}
