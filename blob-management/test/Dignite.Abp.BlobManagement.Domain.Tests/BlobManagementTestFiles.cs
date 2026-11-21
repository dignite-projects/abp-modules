using System;
using System.IO;
using System.Text;
using SkiaSharp;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// Real file content for upload tests: Blob Management detects the type from the bytes and refuses a name whose extension
/// the content contradicts, so a test file has to be what its name says it is. Pictures are drawn and measured with
/// SkiaSharp, the imaging provider the tests run on.
/// </summary>
public static class BlobManagementTestFiles
{
    /// <summary>Enough of a PDF for signature detection: the header and an end marker.</summary>
    public static byte[] Pdf(string text = "not really a pdf, but not an image either")
    {
        return Encoding.ASCII.GetBytes($"%PDF-1.4\n% {text}\n%%EOF\n");
    }

    public static byte[] Text(string text = "plain text")
    {
        return Encoding.UTF8.GetBytes(text);
    }

    /// <summary>
    /// A PNG of noise rather than a flat colour: a flat image compresses so well that it trips the Imaging package's
    /// decode guard on pixels per stored byte before anything else happens.
    /// </summary>
    public static byte[] Png(int width, int height, int seed = 42)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var pixels = new byte[info.BytesSize];
        new Random(seed).NextBytes(pixels);
        for (var alpha = 3; alpha < pixels.Length; alpha += 4)
        {
            pixels[alpha] = 0xFF;
        }

        using var image = SKImage.FromPixelCopy(info, pixels, info.RowBytes);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// <paramref name="png"/> with an <c>acTL</c> chunk (two frames, looping) right after <c>IHDR</c>: what makes a PNG an
    /// APNG. Its CRC is computed, so a decoder that checks it accepts the chunk.
    /// </summary>
    public static byte[] WithAnimationControl(byte[] png)
    {
        byte[] typeAndData = [.. "acTL"u8, 0, 0, 0, 2, 0, 0, 0, 0];
        var crc = 0xFFFFFFFFu;
        foreach (var value in typeAndData)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        crc = ~crc;
        byte[] chunk = [0, 0, 0, 8, .. typeAndData, (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc];

        // Signature (8) + IHDR (25).
        return [.. png[..33], .. chunk, .. png[33..]];
    }

    /// <summary>A JPEG of noise, for the same reason as <see cref="Png"/>.</summary>
    public static byte[] Jpeg(int width, int height, int seed = 42)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var pixels = new byte[info.BytesSize];
        new Random(seed).NextBytes(pixels);
        for (var alpha = 3; alpha < pixels.Length; alpha += 4)
        {
            pixels[alpha] = 0xFF;
        }

        using var image = SKImage.FromPixelCopy(info, pixels, info.RowBytes);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    /// <summary>The encoded format of a picture, read from its header.</summary>
    public static SKEncodedImageFormat Format(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidOperationException("Not a picture SkiaSharp can read.");
        return codec.EncodedFormat;
    }

    /// <summary>The pixel size of a picture, read from its header.</summary>
    public static (int Width, int Height) Measure(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidOperationException("Not a picture SkiaSharp can read.");
        return (codec.Info.Width, codec.Info.Height);
    }

    public static byte[] ReadAll(Stream stream)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
