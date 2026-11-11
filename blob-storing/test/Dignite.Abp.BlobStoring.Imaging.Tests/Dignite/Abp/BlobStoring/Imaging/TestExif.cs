using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// An EXIF orientation added to a real JPEG, PNG or WebP, where a camera or an editor writes it.
/// </summary>
public static class TestExif
{
    /// <summary>
    /// TIFF-structured EXIF data: a header in the given byte order, one directory with one entry, Orientation (0x0112),
    /// SHORT, count 1, and no next directory.
    /// </summary>
    public static byte[] Tiff(int orientation, bool littleEndian = false)
    {
        var bytes = new List<byte>();
        bytes.AddRange(littleEndian ? "II*\0"u8.ToArray() : "MM\0*"u8.ToArray());
        bytes.AddRange(U32(8, littleEndian));
        bytes.AddRange(U16(1, littleEndian));
        bytes.AddRange(U16(0x0112, littleEndian));
        bytes.AddRange(U16(3, littleEndian));
        bytes.AddRange(U32(1, littleEndian));
        bytes.AddRange(U16((ushort)orientation, littleEndian));
        bytes.AddRange([0, 0]);
        bytes.AddRange(U32(0, littleEndian));
        return bytes.ToArray();
    }

    /// <summary><paramref name="jpeg"/> with an APP1 EXIF segment right after SOI.</summary>
    public static byte[] Jpeg(byte[] jpeg, int orientation, bool littleEndian = false)
    {
        var payload = "Exif\0\0"u8.ToArray().Concat(Tiff(orientation, littleEndian)).ToArray();
        var segment = new List<byte> { 0xFF, 0xE1 };
        segment.AddRange(U16((ushort)(payload.Length + 2), littleEndian: false));
        segment.AddRange(payload);
        return jpeg.Take(2).Concat(segment).Concat(jpeg.Skip(2)).ToArray();
    }

    /// <summary><paramref name="png"/> with an <c>eXIf</c> chunk right after IHDR.</summary>
    public static byte[] Png(byte[] png, int orientation)
    {
        var data = Tiff(orientation);
        var chunk = new List<byte>();
        chunk.AddRange(U32((uint)data.Length, littleEndian: false));
        var typeAndData = "eXIf"u8.ToArray().Concat(data).ToArray();
        chunk.AddRange(typeAndData);
        chunk.AddRange(U32(Crc32(typeAndData), littleEndian: false));

        // Signature (8) + IHDR (4 + 4 + 13 + 4).
        const int at = 8 + 25;
        return png.Take(at).Concat(chunk).Concat(png.Skip(at)).ToArray();
    }

    /// <summary>
    /// A simple (<c>VP8 </c> or <c>VP8L</c>) <paramref name="webp"/> of <paramref name="width"/> x
    /// <paramref name="height"/> turned into the extended format with an <c>EXIF</c> chunk after the image, flagged in
    /// <c>VP8X</c> unless <paramref name="flagged"/> is false.
    /// </summary>
    public static byte[] Webp(byte[] webp, int width, int height, int orientation, bool flagged = true)
    {
        var vp8x = new List<byte>();
        vp8x.AddRange("VP8X"u8.ToArray());
        vp8x.AddRange(U32(10, littleEndian: true));
        vp8x.AddRange([flagged ? (byte)0x08 : (byte)0x00, 0, 0, 0]);
        vp8x.AddRange(U24(width - 1));
        vp8x.AddRange(U24(height - 1));

        var exifData = Tiff(orientation, littleEndian: true);
        var exif = new List<byte>();
        exif.AddRange("EXIF"u8.ToArray());
        exif.AddRange(U32((uint)exifData.Length, littleEndian: true));
        exif.AddRange(exifData);
        if (exifData.Length % 2 == 1)
        {
            exif.Add(0);
        }

        var body = "WEBP"u8.ToArray().Concat(vp8x).Concat(webp.Skip(12)).Concat(exif).ToArray();
        return "RIFF"u8.ToArray().Concat(U32((uint)body.Length, littleEndian: true)).Concat(body).ToArray();
    }

    private static byte[] U16(ushort value, bool littleEndian)
    {
        var bytes = new byte[2];
        if (littleEndian)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        }

        return bytes;
    }

    private static byte[] U32(uint value, bool littleEndian)
    {
        var bytes = new byte[4];
        if (littleEndian)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        }

        return bytes;
    }

    private static byte[] U24(int value) => [(byte)value, (byte)(value >> 8), (byte)(value >> 16)];

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }
}
