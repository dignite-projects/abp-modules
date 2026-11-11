using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.Extensions.Options;
using Shouldly;
using SkiaSharp;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Memory;
using Volo.Abp.Imaging;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Abp.BlobStoring.Imaging;

public class JpegToWebpContributor_Tests : AbpIntegratedTest<BlobStoringImagingSkiaSharpTestModule>
{
    private const int Box = BlobStoringImagingSkiaSharpTestModule.Box;

    [Fact]
    public async Task A_Jpeg_Should_Be_Stored_As_Webp()
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.WebpContainer;
        var upload = TestPictures.Noise(300, 200, SKEncodedImageFormat.Jpeg);
        using var source = new MemoryStream(upload);

        await Container(containerName).SaveAsync("photo.jpg", source);

        var stored = await GetStoredBytesAsync(containerName, "photo.jpg");
        TestPictures.Inspect(stored).ShouldBe((SKEncodedImageFormat.Webp, 300, 200, SKEncodedOrigin.TopLeft));
        (await ReadAsync(containerName, "photo.jpg")).ShouldBe(stored);   // one-way: read as stored
        source.CanRead.ShouldBeTrue();                                    // the caller keeps its stream
    }

    /// <summary>
    /// A camera stores a portrait photo sideways and records the turn in its EXIF orientation. The WebP keeps no EXIF, so
    /// the pixels are turned first: the stored picture is upright, with no orientation left for a browser to apply again.
    /// </summary>
    [Theory]
    [InlineData(6, 200, 400, "top")]      // a quarter turn clockwise: the red left half comes out on top
    [InlineData(8, 200, 400, "bottom")]   // a quarter turn anticlockwise: it comes out at the bottom
    [InlineData(3, 400, 200, "right")]    // half a turn: it comes out on the right
    public async Task A_Sideways_Jpeg_Should_Be_Stored_Upright(byte orientation, int width, int height, string red)
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.WebpContainer;
        var upload = TestPictures.WithExifOrientation(TestPictures.RedLeftBlueRightJpeg(400, 200), orientation);

        await Container(containerName).SaveAsync("portrait.jpg", upload);

        var stored = await GetStoredBytesAsync(containerName, "portrait.jpg");
        TestPictures.Inspect(stored).ShouldBe((SKEncodedImageFormat.Webp, width, height, SKEncodedOrigin.TopLeft));
        var (redRegion, blueRegion) = red switch
        {
            "top" => (new SKRectI(50, 50, 150, 150), new SKRectI(50, 250, 150, 350)),
            "bottom" => (new SKRectI(50, 250, 150, 350), new SKRectI(50, 50, 150, 150)),
            _ => (new SKRectI(250, 50, 350, 150), new SKRectI(50, 50, 150, 150))
        };
        ShouldBeRed(TestPictures.AverageColor(stored, redRegion));
        ShouldBeBlue(TestPictures.AverageColor(stored, blueRegion));
    }

    /// <summary>
    /// In the order a Drive's images preset uses - size, types, metadata removal (which keeps the orientation), WebP,
    /// resize - a sideways photo larger than the box is turned, then fitted with its long side, in one encode.
    /// </summary>
    [Fact]
    public async Task A_Sideways_Jpeg_Larger_Than_The_Box_Should_Be_Fitted_Upright()
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.ComposedContainer;
        var upload = TestPictures.WithExifOrientation(TestPictures.RedLeftBlueRightJpeg(1200, 600), 6);

        await Container(containerName).SaveAsync("portrait.jpg", upload);

        var stored = await GetStoredBytesAsync(containerName, "portrait.jpg");
        TestPictures.Inspect(stored).ShouldBe((SKEncodedImageFormat.Webp, Box / 2, Box, SKEncodedOrigin.TopLeft));
        ShouldBeRed(TestPictures.AverageColor(stored, new SKRectI(32, 32, 96, 96)));
        ShouldBeBlue(TestPictures.AverageColor(stored, new SKRectI(32, 160, 96, 224)));
    }

    /// <summary>
    /// With a resize mode other than fitting inside the box, the JPEG is stored upright at full size and the resize
    /// contributor applies its mode to the WebP.
    /// </summary>
    [Fact]
    public async Task With_Another_Resize_Mode_The_Resize_Contributor_Should_Apply_It_To_The_Webp()
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.StretchContainer;

        await Container(containerName).SaveAsync("photo.jpg", TestPictures.Noise(400, 300, SKEncodedImageFormat.Jpeg));

        var stored = await GetStoredBytesAsync(containerName, "photo.jpg");
        TestPictures.Inspect(stored).ShouldBe((SKEncodedImageFormat.Webp, Box, Box, SKEncodedOrigin.TopLeft));
    }

    [Theory]
    [InlineData("drawing.png")]
    [InlineData("picture.webp")]
    [InlineData("animation.gif")]
    public async Task A_Picture_That_Is_Not_A_Jpeg_Should_Be_Stored_As_Uploaded(string blobName)
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.WebpContainer;
        var upload = Path.GetExtension(blobName) switch
        {
            ".png" => TestPictures.Noise(100, 80, SKEncodedImageFormat.Png),
            ".webp" => TestPictures.Noise(100, 80, SKEncodedImageFormat.Webp),
            _ => TestPictures.Gif
        };

        await Container(containerName).SaveAsync(blobName, upload);

        (await GetStoredBytesAsync(containerName, blobName)).ShouldBe(upload);
    }

    [Fact]
    public async Task The_Quality_Should_Be_The_Containers()
    {
        var upload = TestPictures.Noise(300, 200, SKEncodedImageFormat.Jpeg);

        await Container(BlobStoringImagingSkiaSharpTestModule.WebpContainer).SaveAsync("photo.jpg", upload);
        await Container(BlobStoringImagingSkiaSharpTestModule.LowQualityWebpContainer).SaveAsync("photo.jpg", upload);

        var atDefault = await GetStoredBytesAsync(BlobStoringImagingSkiaSharpTestModule.WebpContainer, "photo.jpg");
        var atLow = await GetStoredBytesAsync(BlobStoringImagingSkiaSharpTestModule.LowQualityWebpContainer, "photo.jpg");
        TestPictures.Inspect(atLow).Format.ShouldBe(SKEncodedImageFormat.Webp);
        atLow.Length.ShouldBeLessThan(atDefault.Length);
        GetContainerConfiguration(BlobStoringImagingSkiaSharpTestModule.LowQualityWebpContainer)
            .GetJpegToWebpContributorConfiguration().Quality.ShouldBe(BlobStoringImagingSkiaSharpTestModule.LowQuality);
        GetContainerConfiguration(BlobStoringImagingSkiaSharpTestModule.WebpContainer)
            .GetJpegToWebpContributorConfiguration().Quality.ShouldBe(JpegToWebpContributorConfiguration.DefaultQuality);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void A_Quality_Out_Of_Range_Should_Be_Refused(int quality)
    {
        Should.Throw<AbpException>(() => new BlobContainerConfiguration().StoreJpegAsWebp(quality));
    }

    [Fact]
    public void Storing_Jpeg_As_Webp_Again_Should_Only_Change_The_Quality()
    {
        var container = new BlobContainerConfiguration().StoreJpegAsWebp().StoreJpegAsWebp(60);

        container.PipelineContributors.Count(type => type == typeof(JpegToWebpContributor)).ShouldBe(1);
        container.GetJpegToWebpContributorConfiguration().Quality.ShouldBe(60);
    }

    /// <summary>
    /// What a caller that names its blobs before the save reads: a JPEG is stored as WebP, anything else as it is.
    /// </summary>
    [Fact]
    public void Storing_Jpeg_As_Webp_Should_Declare_The_Stored_Type()
    {
        var container = GetContainerConfiguration(BlobStoringImagingSkiaSharpTestModule.WebpContainer);

        container.GetStoredContentType("image/jpeg").ShouldBe("image/webp");
        container.GetStoredContentType("image/png").ShouldBe("image/png");
        GetContainerConfiguration(BlobStoringImagingSkiaSharpTestModule.ResizeContainer)
            .GetStoredContentType("image/jpeg").ShouldBe("image/jpeg");
    }

    /// <summary>
    /// The resize contributor runs ABP's <see cref="IImageResizer"/>, whose SkiaSharp contributor is replaced: a sideways
    /// JPEG shrunk to fit is stored upright, still a JPEG.
    /// </summary>
    [Fact]
    public async Task A_Sideways_Jpeg_Resized_By_Abps_Resizer_Should_Be_Stored_Upright()
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.ResizeContainer;
        var upload = TestPictures.WithExifOrientation(TestPictures.RedLeftBlueRightJpeg(1200, 600), 6);

        await Container(containerName).SaveAsync("portrait.jpg", upload);

        var stored = await GetStoredBytesAsync(containerName, "portrait.jpg");
        TestPictures.Inspect(stored).ShouldBe((SKEncodedImageFormat.Jpeg, Box / 2, Box, SKEncodedOrigin.TopLeft));
        ShouldBeRed(TestPictures.AverageColor(stored, new SKRectI(32, 32, 96, 96)));
        ShouldBeBlue(TestPictures.AverageColor(stored, new SKRectI(32, 160, 96, 224)));
    }

    [Fact]
    public void The_Module_Should_Replace_Abps_SkiaSharp_Resizer_And_Its_Defaults()
    {
        var contributors = GetRequiredService<System.Collections.Generic.IEnumerable<IImageResizerContributor>>().ToList();
        contributors.ShouldContain(contributor => contributor is UprightSkiaSharpImageResizerContributor);
        contributors.ShouldNotContain(contributor => contributor.GetType() == typeof(SkiaSharpImageResizerContributor));

        var options = GetRequiredService<IOptions<SkiaSharpResizerOptions>>().Value;
        options.SKSamplingOptions.Filter.ShouldBe(SKFilterMode.Linear);
        options.SKSamplingOptions.Mipmap.ShouldBe(SKMipmapMode.Linear);
        options.Quality.ShouldBe(85);
    }

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    private IBlobContainer Container(string name) => GetRequiredService<IBlobContainerFactory>().Create(name);

    private BlobContainerConfiguration GetContainerConfiguration(string name) =>
        GetRequiredService<IBlobContainerConfigurationProvider>().Get(name);

    /// <summary>The bytes the provider stored, bypassing the container's pipeline.</summary>
    private async Task<byte[]> GetStoredBytesAsync(string containerName, string blobName)
    {
        await using var stream = await GetRequiredService<MemoryBlobProvider>()
            .GetOrNullAsync(new BlobProviderGetArgs(containerName, GetContainerConfiguration(containerName), blobName));
        stream.ShouldNotBeNull();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private async Task<byte[]> ReadAsync(string containerName, string blobName)
    {
        await using var stream = await Container(containerName).GetAsync(blobName);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static void ShouldBeRed((int Red, int Green, int Blue) color)
    {
        color.Red.ShouldBeGreaterThan(180);
        color.Blue.ShouldBeLessThan(80);
    }

    private static void ShouldBeBlue((int Red, int Green, int Blue) color)
    {
        color.Blue.ShouldBeGreaterThan(180);
        color.Red.ShouldBeLessThan(80);
    }
}
