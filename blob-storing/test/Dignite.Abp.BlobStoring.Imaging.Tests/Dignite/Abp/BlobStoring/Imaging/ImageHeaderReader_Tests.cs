using System.IO;
using System.Linq;
using Shouldly;
using SkiaSharp;
using Xunit;

namespace Dignite.Abp.BlobStoring.Imaging;

public class ImageHeaderReader_Tests
{
    [Fact]
    public void Should_Read_Png() => Read(TestHeaders.Png(640, 480)).ShouldBe((640, 480));

    [Fact]
    public void Should_Report_Png_Dimensions_Beyond_Int32_As_Int32_MaxValue() =>
        Read(TestHeaders.Png(0x9000_0000, 1)).ShouldBe((int.MaxValue, 1));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_Read_Jpeg_Past_App_Segments(bool progressive) =>
        Read(TestHeaders.Jpeg(1920, 1080, progressive)).ShouldBe((1920, 1080));

    [Fact]
    public void Should_Read_Gif() => Read(TestHeaders.Gif(320, 200)).ShouldBe((320, 200));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_Read_Bmp_Info_Header(bool topDown) => Read(TestHeaders.Bmp(800, 600, topDown)).ShouldBe((800, 600));

    [Fact]
    public void Should_Read_Bmp_Core_Header() => Read(TestHeaders.BmpCore(64, 32)).ShouldBe((64, 32));

    [Fact]
    public void Should_Read_Lossy_Webp() => Read(TestHeaders.WebpLossy(1024, 768)).ShouldBe((1024, 768));

    [Fact]
    public void Should_Read_Lossless_Webp() => Read(TestHeaders.WebpLossless(16383, 9)).ShouldBe((16383, 9));

    [Fact]
    public void Should_Read_Extended_Webp() => Read(TestHeaders.WebpExtended(100000, 3)).ShouldBe((100000, 3));

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void Should_Read_Tiff_In_Both_Byte_Orders(bool littleEndian, bool useLong) =>
        Read(TestHeaders.Tiff(1200, 900, littleEndian, useLong)).ShouldBe((1200, 900));

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public void Should_Agree_With_SkiaSharp_On_Real_Images(SKEncodedImageFormat format) =>
        Read(TestImages.Noise(123, 45, format, quality: 90)).ShouldBe((123, 45));

    [Fact]
    public void Should_Return_Null_For_Content_That_Is_Not_A_Supported_Image()
    {
        Read(TestHeaders.Text).ShouldBeNull();
        Read([]).ShouldBeNull();
    }

    [Fact]
    public void Should_Return_Null_For_A_Truncated_Header()
    {
        Read(TestHeaders.Png(640, 480).Take(20).ToArray()).ShouldBeNull();
        Read(TestHeaders.Jpeg(640, 480).Take(200).ToArray()).ShouldBeNull();
        Read(TestHeaders.Tiff(640, 480, littleEndian: true, useLong: false).Take(14).ToArray()).ShouldBeNull();
    }

    [Fact]
    public void Should_Return_Null_For_A_Jpeg_Without_A_Frame_Header() =>
        Read(TestHeaders.JpegWithoutFrameHeader()).ShouldBeNull();

    [Fact]
    public void Should_Return_Null_For_Zero_Dimensions() => Read(TestHeaders.Png(0, 480)).ShouldBeNull();

    [Fact]
    public void Should_Restore_The_Stream_Position()
    {
        using var stream = new MemoryStream(TestHeaders.Jpeg(10, 20));
        stream.Position = 5;

        ImageHeaderReader.Read(stream).ShouldBe((10, 20));

        stream.Position.ShouldBe(5);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(6, false)]
    [InlineData(8, true)]
    [InlineData(3, true)]
    public void Should_Read_The_Exif_Orientation_Of_A_Jpeg(int orientation, bool littleEndian) =>
        ReadOrientation(TestExif.Jpeg(TestImages.Noise(40, 20, SKEncodedImageFormat.Jpeg), orientation, littleEndian))
            .ShouldBe(orientation);

    /// <summary>
    /// SkiaSharp 3.119 does not apply a PNG's <c>eXIf</c> orientation, so neither does the reader: the resize contributor
    /// must measure a picture the way the decoder that turns it will. If this fails after a SkiaSharp upgrade, SkiaSharp
    /// has started to apply it; read the chunk then (before the first IDAT, where a decoder looks).
    /// </summary>
    [Fact]
    public void Should_Leave_A_Pngs_Exif_Orientation_As_SkiaSharp_Does()
    {
        var png = TestExif.Png(TestImages.Noise(40, 20, SKEncodedImageFormat.Png), 6);

        using var data = SKData.CreateCopy(png);
        using var codec = SKCodec.Create(data);
        codec.ShouldNotBeNull();
        codec.EncodedOrigin.ShouldBe(SKEncodedOrigin.TopLeft);
        ReadOrientation(png).ShouldBeNull();
    }

    [Fact]
    public void Should_Read_The_Exif_Orientation_Of_A_Webp() =>
        ReadOrientation(TestExif.Webp(TestImages.Noise(40, 20, SKEncodedImageFormat.Webp), 40, 20, 8)).ShouldBe(8);

    [Fact]
    public void Should_Not_Read_An_Exif_Chunk_The_Webp_Does_Not_Flag() =>
        ReadOrientation(TestExif.Webp(TestImages.Noise(40, 20, SKEncodedImageFormat.Webp), 40, 20, 8, flagged: false))
            .ShouldBeNull();

    [Fact]
    public void Should_Read_The_Orientation_Of_A_Tiff() => ReadOrientation(TestExif.Tiff(5, littleEndian: true)).ShouldBe(5);

    [Fact]
    public void Should_Return_No_Orientation_Without_Exif()
    {
        ReadOrientation(TestImages.Noise(40, 20, SKEncodedImageFormat.Jpeg)).ShouldBeNull();
        ReadOrientation(TestImages.Noise(40, 20, SKEncodedImageFormat.Png)).ShouldBeNull();
        ReadOrientation(TestHeaders.Gif(40, 20)).ShouldBeNull();
        // An APP1 that is EXIF in name only: its payload is no TIFF structure.
        ReadOrientation(TestHeaders.Jpeg(40, 20)).ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Should_Return_No_Orientation_Out_Of_Range(int orientation) =>
        ReadOrientation(TestExif.Jpeg(TestImages.Noise(40, 20, SKEncodedImageFormat.Jpeg), orientation)).ShouldBeNull();

    [Fact]
    public void Should_Return_No_Orientation_For_Truncated_Exif()
    {
        var jpeg = TestExif.Jpeg(TestImages.Noise(40, 20, SKEncodedImageFormat.Jpeg), 6);

        // SOI (2) + APP1 marker and length (4) + "Exif\0\0" (6) + part of the TIFF header.
        ReadOrientation(jpeg.Take(2 + 4 + 6 + 5).ToArray()).ShouldBeNull();
    }

    /// <summary>
    /// The reader has to agree with the decoder that turns the picture: SkiaSharp's <c>SKCodec.EncodedOrigin</c> is the
    /// EXIF orientation it applies.
    /// </summary>
    [Theory]
    [InlineData("jpeg", 6)]
    [InlineData("jpeg", 3)]
    [InlineData("webp", 6)]
    [InlineData("webp", 7)]
    public void Should_Agree_With_SkiaSharp(string format, int orientation)
    {
        var picture = format switch
        {
            "jpeg" => TestExif.Jpeg(TestImages.Noise(40, 20, SKEncodedImageFormat.Jpeg), orientation),
            _ => TestExif.Webp(TestImages.Noise(40, 20, SKEncodedImageFormat.Webp), 40, 20, orientation)
        };

        using var data = SKData.CreateCopy(picture);
        using var codec = SKCodec.Create(data);
        codec.ShouldNotBeNull();
        ((int)codec.EncodedOrigin).ShouldBe(orientation);
        ReadOrientation(picture).ShouldBe(orientation);
        Read(picture).ShouldBe((40, 20));
    }

    [Fact]
    public void Should_Restore_The_Stream_Position_After_Reading_The_Orientation()
    {
        using var stream = new MemoryStream(TestExif.Jpeg(TestImages.Noise(40, 20, SKEncodedImageFormat.Jpeg), 6));
        stream.Position = 7;

        ImageHeaderReader.ReadExifOrientation(stream).ShouldBe(6);

        stream.Position.ShouldBe(7);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(8, true)]
    public void Quarter_Turns_Should_Swap_The_Sides(int? orientation, bool swaps) =>
        ImageHeaderReader.SwapsSides(orientation).ShouldBe(swaps);

    private static (int Width, int Height)? Read(byte[] content)
    {
        using var stream = new MemoryStream(content);
        return ImageHeaderReader.Read(stream);
    }

    private static int? ReadOrientation(byte[] content)
    {
        using var stream = new MemoryStream(content);
        return ImageHeaderReader.ReadExifOrientation(stream);
    }
}
