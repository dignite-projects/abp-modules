using System;
using System.IO;
using System.IO.Compression;
using SkiaSharp;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Real pictures encoded by SkiaSharp at test time. Noise rather than flat colours: a flat picture compresses so well that
/// it trips the decode guard's pixels-per-byte limit before anything is converted.
/// </summary>
public static class TestPictures
{
    public static byte[] Noise(int width, int height, SKEncodedImageFormat format, int seed = 42)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var pixels = new byte[info.BytesSize];
        new Random(seed).NextBytes(pixels);
        for (var alpha = 3; alpha < pixels.Length; alpha += 4)
        {
            pixels[alpha] = 0xFF;
        }

        return Encode(info, pixels, format);
    }

    /// <summary>
    /// A PNG of noise in which every pixel is partly transparent (alpha 1-255, unpremultiplied), so a conversion that
    /// lost the transparency or the colour under it shows.
    /// </summary>
    public static byte[] TransparentNoisePng(int width, int height, int seed = 42)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var pixels = new byte[info.BytesSize];
        new Random(seed).NextBytes(pixels);
        for (var alpha = 3; alpha < pixels.Length; alpha += 4)
        {
            pixels[alpha] = (byte)Math.Max((byte)1, pixels[alpha]);
        }

        return Encode(info, pixels, SKEncodedImageFormat.Png);
    }

    /// <summary>
    /// An RGB PNG of noise with 16 bits per channel, written by hand: SkiaSharp does not encode 16-bit PNGs. Each row is
    /// a filter byte (none) and the samples, compressed with zlib into one <c>IDAT</c>.
    /// </summary>
    public static byte[] SixteenBitNoisePng(int width, int height, int seed = 42)
    {
        var random = new Random(seed);
        var rowLength = 1 + width * 6;
        var raw = new byte[rowLength * height];
        for (var row = 0; row < height; row++)
        {
            random.NextBytes(raw.AsSpan(row * rowLength + 1, rowLength - 1));
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        byte[] header =
        [
            (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
            (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
            16, 2, 0, 0, 0   // bit depth, colour type (RGB), compression, filter, interlace
        ];

        return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. PngChunk("IHDR"u8, header), .. PngChunk("IDAT"u8, compressed.ToArray()), .. PngChunk("IEND"u8, [])];
    }

    /// <summary>The bits per channel a PNG's <c>IHDR</c> declares.</summary>
    public static int PngBitDepth(byte[] png) => png[24];

    /// <summary>
    /// <paramref name="png"/> with an <c>acTL</c> chunk (two frames, looping) right after <c>IHDR</c>: what makes a PNG an
    /// APNG. Its CRC is computed, so a decoder that checks it accepts the chunk.
    /// </summary>
    public static byte[] WithAnimationControl(byte[] png)
    {
        // Signature (8) + IHDR (25).
        return [.. png[..33], .. PngChunk("acTL"u8, [0, 0, 0, 2, 0, 0, 0, 0]), .. png[33..]];
    }

    /// <summary>A PNG chunk: length, type, data and the CRC of type and data.</summary>
    private static byte[] PngChunk(ReadOnlySpan<byte> type, byte[] data)
    {
        byte[] typeAndData = [.. type, .. data];
        var crc = Crc32(typeAndData);
        var length = data.Length;
        return
        [
            (byte)(length >> 24), (byte)(length >> 16), (byte)(length >> 8), (byte)length,
            .. typeAndData,
            (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc
        ];
    }

    /// <summary>
    /// The pixels of <paramref name="picture"/>, decoded as stored into unpremultiplied RGBA, 8 bits per channel: two
    /// pictures that show the same pixels give the same bytes.
    /// </summary>
    public static byte[] Pixels(byte[] picture)
    {
        using var data = SKData.CreateCopy(picture);
        using var codec = SKCodec.Create(data) ?? throw new InvalidOperationException("Not a picture SkiaSharp can read.");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success)
        {
            throw new InvalidOperationException($"The picture could not be decoded: {result}.");
        }

        return bitmap.Bytes;
    }

    /// <summary>Whether <paramref name="picture"/> has transparency, as its decoder reports it.</summary>
    public static bool HasAlpha(byte[] picture)
    {
        using var data = SKData.CreateCopy(picture);
        using var codec = SKCodec.Create(data) ?? throw new InvalidOperationException("Not a picture SkiaSharp can read.");
        return codec.Info.AlphaType != SKAlphaType.Opaque;
    }

    /// <summary>
    /// A JPEG of noise, mostly red on the left half and mostly blue on the right, so which way it was turned shows.
    /// </summary>
    public static byte[] RedLeftBlueRightJpeg(int width, int height, int seed = 42)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var pixels = new byte[info.BytesSize];
        new Random(seed).NextBytes(pixels);
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            var left = offset / 4 % width < width / 2;
            pixels[offset] = left ? (byte)(200 + pixels[offset] % 56) : (byte)(pixels[offset] % 56);
            pixels[offset + 1] = (byte)(pixels[offset + 1] % 56);
            pixels[offset + 2] = left ? (byte)(pixels[offset + 2] % 56) : (byte)(200 + pixels[offset + 2] % 56);
            pixels[offset + 3] = 0xFF;
        }

        return Encode(info, pixels, SKEncodedImageFormat.Jpeg);
    }

    /// <summary>
    /// <paramref name="jpeg"/> with an EXIF orientation, as a camera records how to turn the photo it stored: an APP1
    /// segment right after SOI - "Exif\0\0", a big-endian TIFF header, one IFD with one entry, Orientation (0x0112), SHORT,
    /// count 1 - and no next IFD. 6 is a portrait taken with the phone turned clockwise: the stored picture is displayed a
    /// quarter turn clockwise.
    /// </summary>
    public static byte[] WithExifOrientation(byte[] jpeg, byte orientation)
    {
        var app1 = new byte[]
        {
            0xFF, 0xE1, 0x00, 0x22,
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00,
            0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x01,
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };
        var result = new byte[jpeg.Length + app1.Length];
        Buffer.BlockCopy(jpeg, 0, result, 0, 2);
        Buffer.BlockCopy(app1, 0, result, 2, app1.Length);
        Buffer.BlockCopy(jpeg, 2, result, 2 + app1.Length, jpeg.Length - 2);
        return result;
    }

    /// <summary>A real 1 x 1 GIF: SkiaSharp decodes GIF but cannot encode it.</summary>
    public static byte[] Gif { get; } =
    [
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0x21, 0xF9, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44, 0x01, 0x00, 0x3B
    ];

    /// <summary>The format, stored size and EXIF origin of a picture, read from its header.</summary>
    public static (SKEncodedImageFormat Format, int Width, int Height, SKEncodedOrigin Origin) Inspect(byte[] picture)
    {
        using var data = SKData.CreateCopy(picture);
        using var codec = SKCodec.Create(data) ?? throw new InvalidOperationException("Not a picture SkiaSharp can read.");
        return (codec.EncodedFormat, codec.Info.Width, codec.Info.Height, codec.EncodedOrigin);
    }

    /// <summary>The average colour of a region of <paramref name="picture"/>, decoded as stored.</summary>
    public static (int Red, int Green, int Blue) AverageColor(byte[] picture, SKRectI region)
    {
        using var bitmap = SKBitmap.Decode(picture) ?? throw new InvalidOperationException("Not a picture SkiaSharp can read.");
        long red = 0, green = 0, blue = 0;
        for (var y = region.Top; y < region.Bottom; y++)
        {
            for (var x = region.Left; x < region.Right; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                red += pixel.Red;
                green += pixel.Green;
                blue += pixel.Blue;
            }
        }

        long count = region.Width * region.Height;
        return ((int)(red / count), (int)(green / count), (int)(blue / count));
    }

    private static byte[] Encode(SKImageInfo info, byte[] pixels, SKEncodedImageFormat format)
    {
        using var image = SKImage.FromPixelCopy(info, pixels, info.RowBytes);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
