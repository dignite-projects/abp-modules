using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Shouldly;
using SkiaSharp;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Memory;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Abp.BlobStoring.Imaging;

public class PngToWebpContributor_Tests : AbpIntegratedTest<BlobStoringImagingSkiaSharpTestModule>
{
    private const int Box = BlobStoringImagingSkiaSharpTestModule.Box;

    /// <summary>Lossless: the WebP shows every pixel the PNG showed.</summary>
    [Fact]
    public async Task A_Png_Should_Be_Stored_As_A_Lossless_Webp()
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.PngWebpContainer;
        var upload = TestPictures.Noise(200, 150, SKEncodedImageFormat.Png);
        upload.Length.ShouldBeGreaterThanOrEqualTo((int)PngToWebpContributorConfiguration.DefaultMinSizeInBytes);
        using var source = new MemoryStream(upload);

        await Container(containerName).SaveAsync("drawing.png", source);

        var stored = await GetStoredBytesAsync(containerName, "drawing.png");
        TestPictures.Inspect(stored).ShouldBe((SKEncodedImageFormat.Webp, 200, 150, SKEncodedOrigin.TopLeft));
        TestPictures.Pixels(stored).ShouldBe(TestPictures.Pixels(upload));
        source.CanRead.ShouldBeTrue();
    }

    /// <summary>WebP keeps transparency, and a partly transparent pixel its colour.</summary>
    [Fact]
    public async Task A_Transparent_Png_Should_Keep_Every_Pixel()
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.PngWebpContainer;
        var upload = TestPictures.TransparentNoisePng(200, 150);

        await Container(containerName).SaveAsync("overlay.png", upload);

        var stored = await GetStoredBytesAsync(containerName, "overlay.png");
        TestPictures.Inspect(stored).Format.ShouldBe(SKEncodedImageFormat.Webp);
        TestPictures.HasAlpha(stored).ShouldBeTrue();
        TestPictures.Pixels(stored).ShouldBe(TestPictures.Pixels(upload));
    }

    /// <summary>
    /// In the order Blob Management's images preset uses, a PNG larger than the box is fitted into it in the same encode,
    /// transparency and all.
    /// </summary>
    [Fact]
    public async Task A_Png_Larger_Than_The_Box_Should_Be_Fitted_Into_It()
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.ComposedContainer;

        await Container(containerName).SaveAsync("wide.png", TestPictures.TransparentNoisePng(600, 300));

        var stored = await GetStoredBytesAsync(containerName, "wide.png");
        TestPictures.Inspect(stored).ShouldBe((SKEncodedImageFormat.Webp, Box, Box / 2, SKEncodedOrigin.TopLeft));
        TestPictures.HasAlpha(stored).ShouldBeTrue();
    }

    /// <summary>Below the size, converting would save next to nothing.</summary>
    [Fact]
    public async Task A_Png_Smaller_Than_The_Size_Should_Be_Stored_As_Uploaded()
    {
        var upload = TestPictures.Noise(20, 20, SKEncodedImageFormat.Png);
        upload.Length.ShouldBeLessThan((int)PngToWebpContributorConfiguration.DefaultMinSizeInBytes);

        await Container(BlobStoringImagingSkiaSharpTestModule.PngWebpContainer).SaveAsync("icon.png", upload);
        await Container(BlobStoringImagingSkiaSharpTestModule.AnyPngWebpContainer).SaveAsync("icon.png", upload);

        (await GetStoredBytesAsync(BlobStoringImagingSkiaSharpTestModule.PngWebpContainer, "icon.png")).ShouldBe(upload);
        TestPictures.Inspect(await GetStoredBytesAsync(BlobStoringImagingSkiaSharpTestModule.AnyPngWebpContainer, "icon.png"))
            .Format.ShouldBe(SKEncodedImageFormat.Webp);
    }

    /// <summary>WebP has 8 bits per channel: a 16-bit PNG would lose precision.</summary>
    [Fact]
    public async Task A_Sixteen_Bit_Png_Should_Be_Stored_As_Uploaded()
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.AnyPngWebpContainer;
        var upload = TestPictures.SixteenBitNoisePng(60, 40);
        TestPictures.PngBitDepth(upload).ShouldBe(16);
        TestPictures.Inspect(upload).ShouldBe((SKEncodedImageFormat.Png, 60, 40, SKEncodedOrigin.TopLeft));

        await Container(containerName).SaveAsync("scan.png", upload);

        (await GetStoredBytesAsync(containerName, "scan.png")).ShouldBe(upload);
    }

    /// <summary>A still WebP would keep one frame of an APNG.</summary>
    [Fact]
    public async Task An_Animated_Png_Should_Be_Stored_As_Uploaded()
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.AnyPngWebpContainer;
        var upload = TestPictures.WithAnimationControl(TestPictures.Noise(200, 150, SKEncodedImageFormat.Png));

        await Container(containerName).SaveAsync("spinner.png", upload);

        (await GetStoredBytesAsync(containerName, "spinner.png")).ShouldBe(upload);
    }

    /// <summary>
    /// With an APNG larger than the box, the resize keeps it as it is too: resized through a still bitmap, it would keep
    /// one frame.
    /// </summary>
    [Fact]
    public async Task An_Animated_Png_Larger_Than_The_Box_Should_Not_Be_Resized()
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.ComposedContainer;
        var upload = TestPictures.WithAnimationControl(TestPictures.Noise(600, 300, SKEncodedImageFormat.Png));

        await Container(containerName).SaveAsync("spinner.png", upload);

        (await GetStoredBytesAsync(containerName, "spinner.png")).ShouldBe(upload);
    }

    /// <summary>
    /// A PNG wider than WebP's 16383 pixels - possible only without a box, and with a decode guard that lets it through -
    /// is stored as a PNG; and so is a JPEG.
    /// </summary>
    [Theory]
    [InlineData("panorama.png")]
    [InlineData("panorama.jpg")]
    public async Task A_Picture_Wider_Than_Webp_Allows_Should_Be_Stored_As_Uploaded(string blobName)
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.UnboxedWebpContainer;
        var upload = TestPictures.Noise(16400, 2, blobName.EndsWith(".png") ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg);

        await Container(containerName).SaveAsync(blobName, upload);

        (await GetStoredBytesAsync(containerName, blobName)).ShouldBe(upload);
    }

    /// <summary>At the limit, it is converted.</summary>
    [Fact]
    public async Task A_Png_As_Wide_As_Webp_Allows_Should_Be_Converted()
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.UnboxedWebpContainer;

        await Container(containerName).SaveAsync("panorama.png", TestPictures.Noise(16383, 2, SKEncodedImageFormat.Png));

        TestPictures.Inspect(await GetStoredBytesAsync(containerName, "panorama.png"))
            .ShouldBe((SKEncodedImageFormat.Webp, 16383, 2, SKEncodedOrigin.TopLeft));
    }

    /// <summary>
    /// What a caller that names its blobs before the save reads is what the container stores: the declaration and the
    /// contributor apply one rule.
    /// </summary>
    [Theory]
    [InlineData("large")]
    [InlineData("small")]
    [InlineData("sixteen-bit")]
    [InlineData("animated")]
    [InlineData("transparent")]
    public async Task The_Declared_Type_Should_Be_The_Stored_Type(string sample)
    {
        const string containerName = BlobStoringImagingSkiaSharpTestModule.PngWebpContainer;
        var upload = sample switch
        {
            "large" => TestPictures.Noise(200, 150, SKEncodedImageFormat.Png),
            "small" => TestPictures.Noise(20, 20, SKEncodedImageFormat.Png),
            "sixteen-bit" => TestPictures.SixteenBitNoisePng(200, 150),
            "animated" => TestPictures.WithAnimationControl(TestPictures.Noise(200, 150, SKEncodedImageFormat.Png)),
            _ => TestPictures.TransparentNoisePng(200, 150)
        };

        string declared;
        using (var content = new MemoryStream(upload))
        {
            declared = GetContainerConfiguration(containerName).GetStoredContentType("image/png", content);
        }

        await Container(containerName).SaveAsync(sample + ".png", upload);

        var stored = TestPictures.Inspect(await GetStoredBytesAsync(containerName, sample + ".png")).Format;
        declared.ShouldBe(stored == SKEncodedImageFormat.Webp ? "image/webp" : "image/png");
        declared.ShouldBe(sample is "large" or "transparent" ? "image/webp" : "image/png");
    }

    [Fact]
    public void A_Negative_Size_Should_Be_Refused()
    {
        Should.Throw<AbpException>(() => new BlobContainerConfiguration().StorePngAsWebp(-1));
    }

    [Fact]
    public void Storing_Png_As_Webp_Again_Should_Only_Change_The_Size()
    {
        var container = new BlobContainerConfiguration().StorePngAsWebp().StorePngAsWebp(1024);

        container.PipelineContributors.Count(type => type == typeof(PngToWebpContributor)).ShouldBe(1);
        container.GetPngToWebpContributorConfiguration().MinSizeInBytes.ShouldBe(1024);
        new BlobContainerConfiguration().StorePngAsWebp().GetPngToWebpContributorConfiguration().MinSizeInBytes
            .ShouldBe(PngToWebpContributorConfiguration.DefaultMinSizeInBytes);
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
}
