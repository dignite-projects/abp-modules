using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using SkiaSharp;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Real images encoded by SkiaSharp at test time.
/// </summary>
public static class TestImages
{
    /// <summary>
    /// An image of random pixels: it does not compress well, so it stays within the decode guard's pixels-per-byte
    /// limit at any size.
    /// </summary>
    public static byte[] Noise(int width, int height, SKEncodedImageFormat format, int quality = 100)
    {
        var random = new Random(width * 7919 + height);
        var pixels = new SKColor[width * height];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
        }

        return Encode(width, height, pixels, format, quality);
    }

    /// <summary>
    /// An image of one colour: it compresses extremely well.
    /// </summary>
    public static byte[] Solid(int width, int height, SKEncodedImageFormat format)
    {
        var pixels = new SKColor[width * height];
        Array.Fill(pixels, new SKColor(0x33, 0x66, 0x99));
        return Encode(width, height, pixels, format, quality: 100);
    }

    public static (int Width, int Height) GetDimensions(byte[] image)
    {
        using var bitmap = SKBitmap.Decode(image);
        if (bitmap == null)
        {
            throw new InvalidOperationException("Not a decodable image.");
        }

        return (bitmap.Width, bitmap.Height);
    }

    public static SKEncodedImageFormat GetFormat(byte[] image)
    {
        using var codec = SKCodec.Create(SKData.CreateCopy(image));
        return codec?.EncodedFormat ?? throw new InvalidOperationException("Not a decodable image.");
    }

    private static byte[] Encode(int width, int height, SKColor[] pixels, SKEncodedImageFormat format, int quality)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        bitmap.Pixels = pixels;
        using var data = bitmap.Encode(format, quality);
        return data.ToArray();
    }
}

/// <summary>
/// Hand-built image headers: just enough bytes for the header reader (and the MIME detector), no pixel data.
/// </summary>
public static class TestHeaders
{
    public static byte[] Png(uint width, uint height)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        bytes.AddRange(BigEndian32(13));
        bytes.AddRange("IHDR"u8.ToArray());
        bytes.AddRange(BigEndian32(width));
        bytes.AddRange(BigEndian32(height));
        bytes.AddRange([8, 6, 0, 0, 0]);        // bit depth, colour type, compression, filter, interlace
        bytes.AddRange([0, 0, 0, 0]);           // CRC (not checked)
        bytes.AddRange(BigEndian32(0));
        bytes.AddRange("IEND"u8.ToArray());
        bytes.AddRange([0xAE, 0x42, 0x60, 0x82]);
        return bytes.ToArray();
    }

    /// <summary>
    /// SOI, JFIF APP0, an EXIF APP1 whose payload is full of 0xFF bytes (so it must be skipped by its length, not
    /// scanned), a DQT, the frame header (SOF0, or SOF2 when <paramref name="progressive"/>), SOS and EOI.
    /// </summary>
    public static byte[] Jpeg(ushort width, ushort height, bool progressive = false)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };

        bytes.AddRange([0xFF, 0xE0, 0x00, 0x10]);
        bytes.AddRange("JFIF\0"u8.ToArray());
        bytes.AddRange([0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);

        var exif = new byte[3000];
        Array.Fill(exif, (byte)0xFF);
        "Exif\0\0"u8.CopyTo(exif);
        bytes.AddRange([0xFF, 0xE1]);
        bytes.AddRange(BigEndian16((ushort)(exif.Length + 2)));
        bytes.AddRange(exif);

        bytes.AddRange([0xFF, 0xDB, 0x00, 0x43, 0x00]);
        bytes.AddRange(new byte[64]);

        bytes.AddRange([0xFF, progressive ? (byte)0xC2 : (byte)0xC0, 0x00, 0x11, 0x08]);
        bytes.AddRange(BigEndian16(height));
        bytes.AddRange(BigEndian16(width));
        bytes.AddRange([0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01]);

        bytes.AddRange([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00, 0x12, 0x34, 0xFF, 0xD9]);
        return bytes.ToArray();
    }

    /// <summary>
    /// A JPEG whose scan starts before any frame header.
    /// </summary>
    public static byte[] JpegWithoutFrameHeader()
    {
        return [0xFF, 0xD8, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00, 0x12, 0x34, 0xFF, 0xD9];
    }

    public static byte[] Gif(ushort width, ushort height)
    {
        var bytes = new List<byte>("GIF89a"u8.ToArray());
        bytes.AddRange(LittleEndian16(width));
        bytes.AddRange(LittleEndian16(height));
        bytes.AddRange([0x00, 0x00, 0x00, 0x3B]);
        return bytes.ToArray();
    }

    /// <summary>
    /// A BMP with a 40-byte BITMAPINFOHEADER; a top-down bitmap stores a negative height.
    /// </summary>
    public static byte[] Bmp(int width, int height, bool topDown = false)
    {
        var bytes = BmpFileHeader(dibHeaderSize: 40);
        bytes.AddRange(LittleEndian32((uint)width));
        bytes.AddRange(LittleEndian32((uint)(topDown ? -height : height)));
        bytes.AddRange([0x01, 0x00, 0x18, 0x00]);  // planes, bits per pixel
        bytes.AddRange(new byte[24]);
        return bytes.ToArray();
    }

    /// <summary>
    /// An OS/2-style BMP with a 12-byte BITMAPCOREHEADER (16-bit dimensions).
    /// </summary>
    public static byte[] BmpCore(ushort width, ushort height)
    {
        var bytes = BmpFileHeader(dibHeaderSize: 12);
        bytes.AddRange(LittleEndian16(width));
        bytes.AddRange(LittleEndian16(height));
        bytes.AddRange([0x01, 0x00, 0x18, 0x00]);
        bytes.AddRange(new byte[8]);
        return bytes.ToArray();
    }

    public static byte[] WebpLossy(ushort width, ushort height)
    {
        var chunk = new List<byte> { 0x50, 0x01, 0x00, 0x9D, 0x01, 0x2A };   // frame tag, start code
        chunk.AddRange(LittleEndian16(width));
        chunk.AddRange(LittleEndian16(height));
        chunk.AddRange(new byte[8]);
        return Riff("VP8 ", chunk);
    }

    public static byte[] WebpLossless(int width, int height)
    {
        var chunk = new List<byte> { 0x2F };
        chunk.AddRange(LittleEndian32((uint)(width - 1) | (uint)(height - 1) << 14));
        chunk.AddRange(new byte[8]);
        return Riff("VP8L", chunk);
    }

    public static byte[] WebpExtended(int width, int height)
    {
        var chunk = new List<byte> { 0x10, 0x00, 0x00, 0x00 };  // flags (alpha), reserved
        chunk.AddRange(LittleEndian32((uint)(width - 1))[..3]);
        chunk.AddRange(LittleEndian32((uint)(height - 1))[..3]);
        return Riff("VP8X", chunk);
    }

    /// <summary>
    /// A TIFF whose first IFD holds NewSubfileType, then ImageWidth and ImageLength as SHORT or LONG values. The
    /// big-endian variant puts the IFD after some padding, so the offset is honoured rather than assumed.
    /// </summary>
    public static byte[] Tiff(uint width, uint height, bool littleEndian, bool useLong)
    {
        var bytes = new List<byte>(littleEndian ? "II*\0"u8.ToArray() : "MM\0*"u8.ToArray());
        var ifdOffset = littleEndian ? 8u : 16u;
        bytes.AddRange(UInt32(ifdOffset, littleEndian));
        while (bytes.Count < ifdOffset)
        {
            bytes.Add(0);
        }

        bytes.AddRange(UInt16(3, littleEndian));
        AddEntry(0x00FE, type: 4, 0);
        AddEntry(0x0100, type: useLong ? (ushort)4 : (ushort)3, width);
        AddEntry(0x0101, type: useLong ? (ushort)4 : (ushort)3, height);
        bytes.AddRange(UInt32(0, littleEndian));   // no next IFD
        return bytes.ToArray();

        void AddEntry(ushort tag, ushort type, uint value)
        {
            bytes.AddRange(UInt16(tag, littleEndian));
            bytes.AddRange(UInt16(type, littleEndian));
            bytes.AddRange(UInt32(1, littleEndian));
            if (type == 3)
            {
                // A SHORT value is left-justified in the 4-byte field.
                bytes.AddRange(UInt16((ushort)value, littleEndian));
                bytes.AddRange([0, 0]);
            }
            else
            {
                bytes.AddRange(UInt32(value, littleEndian));
            }
        }
    }

    public static byte[] Text { get; } = Encoding.UTF8.GetBytes("plain text, definitely not an image header");

    private static List<byte> BmpFileHeader(uint dibHeaderSize)
    {
        var bytes = new List<byte>("BM"u8.ToArray());
        bytes.AddRange(LittleEndian32(0));       // file size (not checked)
        bytes.AddRange(LittleEndian32(0));       // reserved
        bytes.AddRange(LittleEndian32(14 + dibHeaderSize));
        bytes.AddRange(LittleEndian32(dibHeaderSize));
        return bytes;
    }

    private static byte[] Riff(string chunkType, List<byte> chunk)
    {
        var bytes = new List<byte>("RIFF"u8.ToArray());
        bytes.AddRange(LittleEndian32((uint)(4 + 8 + chunk.Count)));
        bytes.AddRange("WEBP"u8.ToArray());
        bytes.AddRange(Encoding.ASCII.GetBytes(chunkType));
        bytes.AddRange(LittleEndian32((uint)chunk.Count));
        bytes.AddRange(chunk);
        return bytes.ToArray();
    }

    private static byte[] UInt16(ushort value, bool littleEndian) => littleEndian ? LittleEndian16(value) : BigEndian16(value);

    private static byte[] UInt32(uint value, bool littleEndian) => littleEndian ? LittleEndian32(value) : BigEndian32(value);

    private static byte[] BigEndian16(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] BigEndian32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] LittleEndian16(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] LittleEndian32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }
}
