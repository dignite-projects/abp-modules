using System;
using System.Buffers.Binary;
using System.IO;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Reads the pixel dimensions of an image from its header, in managed code and without decoding it, so the decode
/// guard can reject an oversized image before an imaging provider allocates memory for it. Understands PNG (IHDR),
/// JPEG (the SOF segment, walking past APP and other segments), GIF (logical screen), BMP (12-byte core and 40-byte
/// and larger info headers), WebP (<c>VP8 </c>, <c>VP8L</c>, <c>VP8X</c>) and TIFF (the first IFD, either byte
/// order). Anything else, or a header it cannot make sense of, yields <c>null</c>. Also reads the EXIF orientation
/// (<see cref="ReadExifOrientation"/>), which decides whether the image is displayed with its sides swapped.
/// <para>
/// Public so that code outside the pipeline can apply the same bound before it decodes an image, such as an endpoint
/// that resizes a stored image on request.
/// </para>
/// </summary>
public static class ImageHeaderReader
{
    // Bounds for the walks through JPEG segments and TIFF directory entries, so a hostile header cannot
    // make the reader loop for long.
    private const int MaxJpegSegments = 1024;
    private const int MaxTiffEntries = 4096;
    private const int MaxWebpChunks = 1024;

    /// <summary>
    /// The dimensions declared by the header of the image in <paramref name="stream"/>, which must be seekable; it is
    /// read from position 0 and its position is restored afterwards. A dimension larger than <see cref="int.MaxValue"/>
    /// is reported as <see cref="int.MaxValue"/>. <c>null</c> when the format is not one of the supported ones or the
    /// header is truncated or invalid.
    /// </summary>
    public static (int Width, int Height)? Read(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("The stream must be seekable so its header can be read.", nameof(stream));
        }

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            return ReadFromStart(stream);
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static (int Width, int Height)? ReadFromStart(Stream stream)
    {
        Span<byte> header = stackalloc byte[32];
        var length = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        header = header[..length];

        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return ReadPng(header);
        }

        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8]))
        {
            return ReadJpeg(stream);
        }

        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
        {
            return header.Length < 10
                ? null
                : Create(BinaryPrimitives.ReadUInt16LittleEndian(header[6..]), BinaryPrimitives.ReadUInt16LittleEndian(header[8..]));
        }

        if (header.StartsWith("BM"u8))
        {
            return ReadBmp(header);
        }

        if (header.Length >= 16 && header.StartsWith("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return ReadWebp(header);
        }

        if (header.StartsWith("II*\0"u8))
        {
            return ReadTiff(stream, header, littleEndian: true);
        }

        if (header.StartsWith("MM\0*"u8))
        {
            return ReadTiff(stream, header, littleEndian: false);
        }

        return null;
    }

    private static (int Width, int Height)? ReadPng(ReadOnlySpan<byte> header)
    {
        // Signature (8), then the IHDR chunk: length (4), type (4), width (4, big-endian), height (4).
        if (header.Length < 24 || !header.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }

        return Create(BinaryPrimitives.ReadUInt32BigEndian(header[16..]), BinaryPrimitives.ReadUInt32BigEndian(header[20..]));
    }

    private static (int Width, int Height)? ReadJpeg(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[7];
        long position = 2; // after SOI

        for (var segment = 0; segment < MaxJpegSegments; segment++)
        {
            stream.Position = position;
            if (stream.ReadByte() != 0xFF)
            {
                return null;
            }

            int marker;
            do
            {
                marker = stream.ReadByte(); // 0xFF fill bytes may precede a marker
            }
            while (marker == 0xFF);

            if (marker < 0)
            {
                return null;
            }

            if (marker is 0x01 or 0xD8 or >= 0xD0 and <= 0xD7)
            {
                // TEM, SOI and RSTn stand alone, without a length.
                position = stream.Position;
                continue;
            }

            if (marker is 0xD9 or 0xDA)
            {
                // EOI, or the scan data starts: no frame header came before it.
                return null;
            }

            stream.ReadExactly(buffer[..2]);
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(buffer);
            if (segmentLength < 2)
            {
                return null;
            }

            if (IsStartOfFrame(marker))
            {
                // Sample precision (1), number of lines (2), samples per line (2).
                stream.ReadExactly(buffer[..5]);
                return Create(BinaryPrimitives.ReadUInt16BigEndian(buffer[3..]), BinaryPrimitives.ReadUInt16BigEndian(buffer[1..]));
            }

            // APPn (EXIF, ICC profiles, ...), DQT, DHT, COM, ...: skip the segment.
            position = stream.Position - 2 + segmentLength;
        }

        return null;
    }

    private static bool IsStartOfFrame(int marker)
    {
        // SOF0-SOF15, except DHT (C4), JPG (C8) and DAC (CC), which share the range.
        return marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;
    }

    private static (int Width, int Height)? ReadBmp(ReadOnlySpan<byte> header)
    {
        if (header.Length < 26)
        {
            return null;
        }

        var dibHeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(header[14..]);
        if (dibHeaderSize == 12)
        {
            // BITMAPCOREHEADER: 16-bit unsigned width and height.
            return Create(BinaryPrimitives.ReadUInt16LittleEndian(header[18..]), BinaryPrimitives.ReadUInt16LittleEndian(header[20..]));
        }

        if (dibHeaderSize >= 40)
        {
            // BITMAPINFOHEADER and later: 32-bit signed width and height; a negative height means top-down rows.
            var width = BinaryPrimitives.ReadInt32LittleEndian(header[18..]);
            var height = BinaryPrimitives.ReadInt32LittleEndian(header[22..]);
            if (width <= 0 || height == int.MinValue)
            {
                return null;
            }

            return Create((uint)width, (uint)Math.Abs(height));
        }

        return null;
    }

    private static (int Width, int Height)? ReadWebp(ReadOnlySpan<byte> header)
    {
        var chunk = header.Slice(12, 4);

        if (chunk.SequenceEqual("VP8 "u8))
        {
            // Lossy: chunk data at 20 starts with a 3-byte frame tag, then the start code 9D 01 2A, then 14-bit
            // width and height (the top 2 bits are the scale).
            if (header.Length < 30 || !header.Slice(23, 3).SequenceEqual((ReadOnlySpan<byte>)[0x9D, 0x01, 0x2A]))
            {
                return null;
            }

            return Create(
                (uint)(BinaryPrimitives.ReadUInt16LittleEndian(header[26..]) & 0x3FFF),
                (uint)(BinaryPrimitives.ReadUInt16LittleEndian(header[28..]) & 0x3FFF));
        }

        if (chunk.SequenceEqual("VP8L"u8))
        {
            // Lossless: signature byte 0x2F, then 14 bits of width - 1 and 14 bits of height - 1.
            if (header.Length < 25 || header[20] != 0x2F)
            {
                return null;
            }

            var bits = BinaryPrimitives.ReadUInt32LittleEndian(header[21..]);
            return Create((bits & 0x3FFF) + 1, ((bits >> 14) & 0x3FFF) + 1);
        }

        if (chunk.SequenceEqual("VP8X"u8))
        {
            // Extended: flags (1), reserved (3), then 24-bit canvas width - 1 and height - 1.
            if (header.Length < 30)
            {
                return null;
            }

            return Create(ReadUInt24LittleEndian(header[24..]) + 1, ReadUInt24LittleEndian(header[27..]) + 1);
        }

        return null;
    }

    private static (int Width, int Height)? ReadTiff(Stream stream, ReadOnlySpan<byte> header, bool littleEndian)
    {
        if (header.Length < 8)
        {
            return null;
        }

        var firstIfdOffset = ReadUInt32(header[4..], littleEndian);
        stream.Position = firstIfdOffset;

        Span<byte> buffer = stackalloc byte[12];
        stream.ReadExactly(buffer[..2]);
        var entryCount = Math.Min((int)ReadUInt16(buffer, littleEndian), MaxTiffEntries);

        uint? width = null;
        uint? height = null;
        for (var i = 0; i < entryCount && (width == null || height == null); i++)
        {
            // Tag (2), type (2), count (4), value or offset (4); a SHORT value is in the first 2 bytes of the field.
            stream.ReadExactly(buffer);
            var tag = ReadUInt16(buffer, littleEndian);
            if (tag is not (0x0100 or 0x0101))
            {
                continue;
            }

            uint? value = ReadUInt16(buffer[2..], littleEndian) switch
            {
                3 => ReadUInt16(buffer[8..], littleEndian),  // SHORT
                4 => ReadUInt32(buffer[8..], littleEndian),  // LONG
                _ => null
            };

            if (tag == 0x0100)
            {
                width = value;
            }
            else
            {
                height = value;
            }
        }

        return width.HasValue && height.HasValue ? Create(width.Value, height.Value) : null;
    }

    /// <summary>
    /// The EXIF orientation (1-8) of the image in <paramref name="stream"/>, which must be seekable; it is read from
    /// position 0 and its position is restored afterwards. A camera stores a portrait photo sideways and records here how
    /// to turn it for display: 5-8 are quarter turns, so the picture is displayed with its width and height swapped
    /// (<see cref="SwapsSides"/>). <c>null</c> when the image carries none, the format is not one of those read, or the
    /// EXIF data is truncated or invalid.
    /// <para>
    /// Read where SkiaSharp reads it, because what it decides has to match the decoder that turns the picture: a JPEG's
    /// first <c>APP1</c> EXIF segment before the scan, a WebP's <c>EXIF</c> chunk (extended format), a TIFF's first
    /// directory. Only the first directory of the EXIF data is searched, as the orientation tag lives there. A PNG's
    /// <c>eXIf</c> chunk is not read: SkiaSharp 3.119 (ABP 10.7) does not apply it, so a PNG is resized as stored.
    /// </para>
    /// </summary>
    public static int? ReadExifOrientation(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("The stream must be seekable so its header can be read.", nameof(stream));
        }

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            Span<byte> header = stackalloc byte[32];
            var length = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
            header = header[..length];

            if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8]))
            {
                return ReadJpegOrientation(stream);
            }

            if (header.Length >= 21 && header.StartsWith("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
            {
                return ReadWebpOrientation(stream, header);
            }

            if (header.StartsWith("II*\0"u8) || header.StartsWith("MM\0*"u8))
            {
                return ReadExifTiffOrientation(stream, 0, stream.Length);
            }

            return null;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>
    /// Whether an image with the EXIF <paramref name="orientation"/> is displayed with its width and height swapped: the
    /// quarter turns, 5-8.
    /// </summary>
    public static bool SwapsSides(int? orientation)
    {
        return orientation is >= 5 and <= 8;
    }

    private static int? ReadJpegOrientation(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[6];
        long position = 2; // after SOI

        for (var segment = 0; segment < MaxJpegSegments; segment++)
        {
            stream.Position = position;
            if (stream.ReadByte() != 0xFF)
            {
                return null;
            }

            int marker;
            do
            {
                marker = stream.ReadByte();
            }
            while (marker == 0xFF);

            if (marker < 0 || marker is 0xD9 or 0xDA)
            {
                // The end, or the scan data: no EXIF segment came before it.
                return null;
            }

            if (marker is 0x01 or 0xD8 or >= 0xD0 and <= 0xD7)
            {
                position = stream.Position;
                continue;
            }

            stream.ReadExactly(buffer[..2]);
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(buffer);
            if (segmentLength < 2)
            {
                return null;
            }

            var dataStart = stream.Position;
            if (marker == 0xE1 && segmentLength >= 2 + 6 + 8)
            {
                stream.ReadExactly(buffer);
                if (buffer.SequenceEqual("Exif\0\0"u8))
                {
                    // The first EXIF segment is the one read, whatever it holds.
                    return ReadExifTiffOrientation(stream, dataStart + 6, segmentLength - 2 - 6);
                }
            }

            position = dataStart - 2 + segmentLength;
        }

        return null;
    }

    private static int? ReadWebpOrientation(Stream stream, ReadOnlySpan<byte> header)
    {
        // Only the extended format carries EXIF, flagged in the VP8X chunk's first byte.
        if (!header.Slice(12, 4).SequenceEqual("VP8X"u8) || (header[20] & 0x08) == 0)
        {
            return null;
        }

        var end = Math.Min(stream.Length, 8L + BinaryPrimitives.ReadUInt32LittleEndian(header[4..]));
        Span<byte> buffer = stackalloc byte[8];
        long position = 12;

        for (var chunk = 0; chunk < MaxWebpChunks && position + 8 <= end; chunk++)
        {
            stream.Position = position;
            stream.ReadExactly(buffer);
            var chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer[4..]);
            var dataStart = position + 8;

            if (buffer[..4].SequenceEqual("EXIF"u8))
            {
                return ReadExifTiffOrientation(stream, dataStart, Math.Min(chunkLength, end - dataStart), allowExifPrefix: true);
            }

            position = dataStart + chunkLength + (chunkLength & 1); // chunks are padded to an even length
        }

        return null;
    }

    /// <summary>
    /// The orientation tag (0x0112) of the first directory of the TIFF-structured EXIF data at <paramref name="start"/>,
    /// <paramref name="length"/> bytes long. <paramref name="allowExifPrefix"/>: the data may begin with <c>Exif\0\0</c>,
    /// as some writers of WebP EXIF chunks put there.
    /// </summary>
    private static int? ReadExifTiffOrientation(Stream stream, long start, long length, bool allowExifPrefix = false)
    {
        Span<byte> buffer = stackalloc byte[12];
        if (length < 8)
        {
            return null;
        }

        stream.Position = start;
        stream.ReadExactly(buffer[..8]);
        if (allowExifPrefix && buffer[..6].SequenceEqual("Exif\0\0"u8))
        {
            start += 6;
            length -= 6;
            if (length < 8)
            {
                return null;
            }

            stream.Position = start;
            stream.ReadExactly(buffer[..8]);
        }

        bool littleEndian;
        if (buffer[..4].SequenceEqual("II*\0"u8))
        {
            littleEndian = true;
        }
        else if (buffer[..4].SequenceEqual("MM\0*"u8))
        {
            littleEndian = false;
        }
        else
        {
            return null;
        }

        var ifdOffset = ReadUInt32(buffer[4..], littleEndian);
        if (ifdOffset < 8 || ifdOffset + 2 > length)
        {
            return null;
        }

        stream.Position = start + ifdOffset;
        stream.ReadExactly(buffer[..2]);
        var entryCount = Math.Min((int)ReadUInt16(buffer, littleEndian), MaxTiffEntries);
        entryCount = (int)Math.Min(entryCount, (length - ifdOffset - 2) / 12);

        for (var i = 0; i < entryCount; i++)
        {
            // Tag (2), type (2), count (4), value (4): a SHORT value is in the first 2 bytes of the field.
            stream.ReadExactly(buffer);
            if (ReadUInt16(buffer, littleEndian) != 0x0112)
            {
                continue;
            }

            if (ReadUInt16(buffer[2..], littleEndian) != 3)
            {
                return null;
            }

            var orientation = ReadUInt16(buffer[8..], littleEndian);
            return orientation is >= 1 and <= 8 ? orientation : null;
        }

        return null;
    }

    private static (int Width, int Height)? Create(uint width, uint height)
    {
        if (width == 0 || height == 0)
        {
            return null;
        }

        return ((int)Math.Min(width, int.MaxValue), (int)Math.Min(height, int.MaxValue));
    }

    private static uint ReadUInt24LittleEndian(ReadOnlySpan<byte> bytes)
    {
        return (uint)(bytes[0] | bytes[1] << 8 | bytes[2] << 16);
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, bool littleEndian)
    {
        return littleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, bool littleEndian)
    {
        return littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }
}
