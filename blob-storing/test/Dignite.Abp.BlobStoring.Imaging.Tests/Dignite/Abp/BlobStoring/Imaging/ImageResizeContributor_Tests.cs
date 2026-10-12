using System.Text;
using System.Threading.Tasks;
using Shouldly;
using SkiaSharp;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Imaging;
using Xunit;

namespace Dignite.Abp.BlobStoring.Imaging;

public class ImageResizeContributor_Tests : BlobStoringImagingTestBase
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "photo.jpg", 800, 400, 200, 100)]
    [InlineData(SKEncodedImageFormat.Png, "photo.png", 300, 600, 100, 200)]
    [InlineData(SKEncodedImageFormat.Webp, "photo.webp", 500, 500, 200, 200)]
    public async Task Should_Shrink_Into_The_Box_Keeping_The_Aspect_Ratio(
        SKEncodedImageFormat format, string blobName, int width, int height, int expectedWidth, int expectedHeight)
    {
        const string containerName = BlobStoringImagingTestModule.ResizeContainer;
        var source = new TrackingStream(TestImages.Noise(width, height, format, quality: 90), canSeek: true);

        await Container(containerName).SaveAsync(blobName, source);

        var stored = await GetStoredBytesAsync(containerName, blobName);
        stored.ShouldNotBeNull();
        TestImages.GetDimensions(stored).ShouldBe((expectedWidth, expectedHeight));
        TestImages.GetFormat(stored).ShouldBe(format);
        (await ReadAllAsync(await Container(containerName).GetAsync(blobName))).ShouldBe(stored);
        source.IsDisposed.ShouldBeFalse(); // the caller keeps its stream
    }

    [Fact]
    public async Task Should_Constrain_Only_The_Configured_Dimension()
    {
        const string containerName = BlobStoringImagingTestModule.ResizeWidthContainer;

        await Container(containerName).SaveAsync("wide.jpg", TestImages.Noise(600, 1000, SKEncodedImageFormat.Jpeg, quality: 90));

        var stored = await GetStoredBytesAsync(containerName, "wide.jpg");
        stored.ShouldNotBeNull();
        TestImages.GetDimensions(stored).ShouldBe((BlobStoringImagingTestModule.ResizeMaxWidth, 500));
    }

    [Theory]
    [InlineData(150, 80)]
    [InlineData(200, 200)]
    public async Task Should_Store_An_Image_That_Already_Fits_Unchanged(int width, int height)
    {
        const string containerName = BlobStoringImagingTestModule.ResizeContainer;
        var content = TestImages.Noise(width, height, SKEncodedImageFormat.Png);

        await Container(containerName).SaveAsync("small.png", content);

        // Not re-encoded, and above all not upscaled.
        (await GetStoredBytesAsync(containerName, "small.png")).ShouldBe(content);
    }

    [Fact]
    public async Task Should_Resize_A_Non_Seekable_Stream()
    {
        const string containerName = BlobStoringImagingTestModule.ResizeContainer;
        var source = new TrackingStream(TestImages.Noise(400, 300, SKEncodedImageFormat.Jpeg, quality: 90), canSeek: false);

        await Container(containerName).SaveAsync("stream.jpg", source);

        var stored = await GetStoredBytesAsync(containerName, "stream.jpg");
        stored.ShouldNotBeNull();
        TestImages.GetDimensions(stored).ShouldBe((200, 150));
        source.IsDisposed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(BlobStoringImagingTestModule.MinWidth - 1, 500)]
    [InlineData(500, BlobStoringImagingTestModule.MinHeight - 1)]
    public async Task Should_Reject_An_Image_Below_The_Minimum_Size(int width, int height)
    {
        const string containerName = BlobStoringImagingTestModule.MinimumSizeContainer;

        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(containerName).SaveAsync("tiny.png", TestImages.Noise(width, height, SKEncodedImageFormat.Png)));

        exception.Code.ShouldBe(BlobStoringImagingErrorCodes.ImageTooSmall);
        exception.Data["Width"].ShouldBe(width);
        exception.Data["Height"].ShouldBe(height);
        exception.Data["MinWidth"].ShouldBe(BlobStoringImagingTestModule.MinWidth);
        exception.Data["MinHeight"].ShouldBe(BlobStoringImagingTestModule.MinHeight);
        (await GetStoredBytesAsync(containerName, "tiny.png")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Accept_An_Image_Of_Exactly_The_Minimum_Size()
    {
        const string containerName = BlobStoringImagingTestModule.MinimumSizeContainer;
        var content = TestImages.Noise(BlobStoringImagingTestModule.MinWidth, BlobStoringImagingTestModule.MinHeight, SKEncodedImageFormat.Png);

        await Container(containerName).SaveAsync("exact.png", content);

        (await GetStoredBytesAsync(containerName, "exact.png")).ShouldBe(content);
    }

    [Fact]
    public async Task Should_Pass_Content_That_Is_Not_An_Image_Through_Byte_For_Byte()
    {
        const string containerName = BlobStoringImagingTestModule.MinimumSizeContainer;
        var content = Encoding.UTF8.GetBytes("not an image, and no minimum size applies to it");

        await Container(containerName).SaveAsync("notes.txt", new TrackingStream(content, canSeek: false));

        (await GetStoredBytesAsync(containerName, "notes.txt")).ShouldBe(content);
    }

    [Fact]
    public async Task Should_Pass_An_Image_The_Provider_Does_Not_Support_Through()
    {
        // SkiaSharp's resizer handles JPEG, PNG and WebP only. A 24-bit BMP wider than the box, with its pixel data.
        const string containerName = BlobStoringImagingTestModule.ResizeContainer;
        byte[] content = [.. TestHeaders.Bmp(300, 10), .. new byte[300 * 10 * 3]];

        await Container(containerName).SaveAsync("legacy.bmp", content);

        (await GetStoredBytesAsync(containerName, "legacy.bmp")).ShouldBe(content);
    }

    [Fact]
    public void AddImageResizeContributor_Should_Reject_An_Invalid_Configuration()
    {
        var configuration = new BlobContainerConfiguration();

        Should.Throw<AbpException>(() => configuration.AddImageResizeContributor(_ => { }));
        Should.Throw<AbpException>(() => configuration.AddImageResizeContributor(c => c.MaxWidth = -1));
        Should.Throw<AbpException>(() => configuration.AddImageResizeContributor(c =>
        {
            c.MaxWidth = 100;
            c.MinHeight = -1;
        }));
        Should.Throw<AbpException>(() => configuration.AddImageResizeContributor(c =>
        {
            c.MaxWidth = 100;
            c.Mode = (ImageResizeMode)42;
        }));
        configuration.PipelineContributors.ShouldBeEmpty();
    }

    [Fact]
    public void AddImageResizeContributor_Should_Default_To_Max_And_Add_The_Contributor_Once()
    {
        var configuration = new BlobContainerConfiguration();

        configuration.AddImageResizeContributor(c => c.MaxHeight = 10);
        configuration.AddImageResizeContributor(c => c.MaxWidth = 20);

        configuration.PipelineContributors.ShouldBe([typeof(ImageResizeContributor)]);
        var resize = configuration.GetImageResizeContributorConfiguration();
        resize.MaxWidth.ShouldBe(20);
        resize.MaxHeight.ShouldBe(10);
        resize.MinWidth.ShouldBe(0);
        resize.MinHeight.ShouldBe(0);
        resize.Mode.ShouldBe(ImageResizeMode.Max);
    }
}
