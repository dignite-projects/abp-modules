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

    private static (int Width, int Height)? Read(byte[] content)
    {
        using var stream = new MemoryStream(content);
        return ImageHeaderReader.Read(stream);
    }
}
