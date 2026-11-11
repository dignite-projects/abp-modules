using System;
using System.IO;
using System.Text;
using SkiaSharp;

namespace Dignite.Drive;

/// <summary>
/// Real file content for upload tests: the Drive detects the type from the bytes and refuses a name whose extension
/// the content contradicts, so a test file has to be what its name says it is. Pictures are drawn and measured with
/// SkiaSharp, the imaging provider the tests run on.
/// </summary>
public static class DriveTestFiles
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
