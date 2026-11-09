using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The format work of <see cref="ImageMetadataStripContributor"/>: removes the metadata that editing software piles
/// into an image and that nothing displaying it reads, without touching the image itself. The segments or chunks are
/// dropped and every other byte is copied as it is, so the pixels, the colour profile and the orientation are exactly
/// those saved. Managed code only, no image library, and nothing is decoded.
/// <para>
/// <b>JPEG.</b> Dropped: Extended XMP (APP1 <c>http://ns.adobe.com/xmp/extension/</c>), the Photoshop image
/// resources (APP13 <c>Photoshop 3.0</c>) and comments (COM). Extended XMP is where Photoshop spills an XMP packet
/// beyond the 64 KB one segment holds - in practice <c>photoshop:DocumentAncestors</c>, the ID of every document
/// ever pasted or placed into the file and into those, which reached 18 MB in a 1984 x 935 banner whose image
/// was 0.9 MB. Kept: the main XMP packet (one segment, at most 64 KB, where an HDR gain map and the rights
/// statement are declared), EXIF (the orientation), the ICC profile, JFIF, the Adobe segment (APP14, which says how
/// the decoder converts the colours) and anything after the image (a multi-picture file's other pictures).
/// </para>
/// <para>
/// <b>PNG.</b> Dropped: the text chunks (<c>tEXt</c>, <c>zTXt</c>, <c>iTXt</c> - Photoshop's XMP goes into an
/// <c>iTXt</c>) and <c>tIME</c>. Kept: every other chunk, the colour space, <c>eXIf</c> and an APNG's frames among
/// them.
/// </para>
/// <para>
/// Anything else - another format, or a file whose structure does not parse - is left unchanged.
/// </para>
/// </summary>
internal static class ImageMetadataStripper
{
    private static readonly byte[] ExtendedXmpSignature = Encoding.ASCII.GetBytes("http://ns.adobe.com/xmp/extension/\0");
    private static readonly byte[] PhotoshopSignature = Encoding.ASCII.GetBytes("Photoshop 3.0\0");
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>
    /// <paramref name="content"/> without its removable metadata, or <c>null</c> when there is none to remove or
    /// the content is not a JPEG or PNG this can parse.
    /// </summary>
    public static byte[]? Strip(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return StripJpeg(content);
        }

        if (content.StartsWith(PngSignature))
        {
            return StripPng(content);
        }

        return null;
    }

    private static byte[]? StripJpeg(ReadOnlySpan<byte> content)
    {
        using var output = new MemoryStream(content.Length);
        output.Write(content[..2]); // SOI
        var removed = false;
        var position = 2;

        while (true)
        {
            // Fill bytes (0xFF) may pad the space before a marker.
            while (position < content.Length && content[position] == 0xFF && position + 1 < content.Length && content[position + 1] == 0xFF)
            {
                position++;
            }

            if (position + 4 > content.Length || content[position] != 0xFF)
            {
                return null;
            }

            var marker = content[position + 1];
            if (marker is 0xD9 or 0x01 or (>= 0xD0 and <= 0xD7))
            {
                // EOI, TEM or a restart marker before the scan: not a picture this parses.
                return null;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(position + 2, 2));
            if (length < 2 || position + 2 + length > content.Length)
            {
                return null;
            }

            if (marker == 0xDA)
            {
                // Start of scan: the entropy-coded data, any further scans and whatever follows the image are
                // copied as they are.
                output.Write(content[position..]);
                break;
            }

            var segment = content.Slice(position, 2 + length);
            var body = segment[4..];
            var drop = marker switch
            {
                0xE1 => body.StartsWith(ExtendedXmpSignature),
                0xED => body.StartsWith(PhotoshopSignature),
                0xFE => true,
                _ => false
            };

            if (drop)
            {
                removed = true;
            }
            else
            {
                output.Write(segment);
            }

            position += 2 + length;
        }

        return removed ? output.ToArray() : null;
    }

    private static byte[]? StripPng(ReadOnlySpan<byte> content)
    {
        using var output = new MemoryStream(content.Length);
        output.Write(content[..PngSignature.Length]);
        var removed = false;
        var position = PngSignature.Length;

        while (true)
        {
            if (position + 12 > content.Length)
            {
                return null;
            }

            var length = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(position, 4));
            if (length > int.MaxValue - 12 || position + 12 + (long)length > content.Length)
            {
                return null;
            }

            var chunk = content.Slice(position, 12 + (int)length);
            var type = Encoding.ASCII.GetString(chunk.Slice(4, 4));
            if (type is "tEXt" or "zTXt" or "iTXt" or "tIME")
            {
                removed = true;
            }
            else
            {
                output.Write(chunk);
            }

            position += chunk.Length;
            if (type == "IEND")
            {
                // Bytes after IEND are not part of the picture; keep them, as the JPEG path keeps a trailer.
                output.Write(content[position..]);
                break;
            }
        }

        return removed ? output.ToArray() : null;
    }
}
