using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Abp.FileStoring.Imaging;

public class ImageResizeHandler_Tests : AbpIntegratedTest<ImagingTestModule>
{
    [Fact]
    public async Task ExecuteAsync_Should_Resize_Image_Larger_Than_Preset()
    {
        var handler = GetRequiredService<ImageResizeHandler>();

        var configuration = new BlobContainerConfiguration();
        configuration.AddImageResizeHandler(options =>
        {
            options.ImageWidth = 200;
            options.ImageHeight = 200;
        });

        var originalStream = new MemoryStream();
        using (var originalImage = new Image<Rgba32>(800, 600))
        {
            await originalImage.SaveAsJpegAsync(originalStream);
        }
        originalStream.Position = 0;

        var context = new FileHandlerContext("photo.jpg", "image/jpeg", originalStream, configuration);

        await handler.ExecuteAsync(context);

        context.BlobStream.Position = 0;
        using (var resizedImage = await Image.LoadAsync(context.BlobStream))
        {
            resizedImage.Width.ShouldBeLessThanOrEqualTo(200);
            resizedImage.Height.ShouldBeLessThanOrEqualTo(200);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Should_Not_Touch_Image_Already_Within_Preset()
    {
        var handler = GetRequiredService<ImageResizeHandler>();

        var configuration = new BlobContainerConfiguration();
        configuration.AddImageResizeHandler(options =>
        {
            options.ImageWidth = 800;
            options.ImageHeight = 600;
        });

        var originalStream = new MemoryStream();
        using (var originalImage = new Image<Rgba32>(200, 150))
        {
            await originalImage.SaveAsJpegAsync(originalStream);
        }
        originalStream.Position = 0;

        var context = new FileHandlerContext("small.jpg", "image/jpeg", originalStream, configuration);

        await handler.ExecuteAsync(context);

        context.BlobStream.Position = 0;
        using (var resultImage = await Image.LoadAsync(context.BlobStream))
        {
            resultImage.Width.ShouldBe(200);
            resultImage.Height.ShouldBe(150);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Should_Reject_Image_Smaller_Than_Preset_When_Required()
    {
        var handler = GetRequiredService<ImageResizeHandler>();

        var configuration = new BlobContainerConfiguration();
        configuration.AddImageResizeHandler(options =>
        {
            options.ImageWidth = 2000;
            options.ImageHeight = 2000;
            options.ImageSizeMustBeLargerThanPreset = true;
        });

        var stream = new MemoryStream();
        using (var smallImage = new Image<Rgba32>(100, 100))
        {
            await smallImage.SaveAsJpegAsync(stream);
        }
        stream.Position = 0;

        var context = new FileHandlerContext("small.jpg", "image/jpeg", stream, configuration);

        var exception = await Should.ThrowAsync<BusinessException>(() => handler.ExecuteAsync(context));
        exception.Code.ShouldBe(FileStoringImagingErrorCodes.ImageSizeTooSmall);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Ignore_Non_Image_Mime_Types()
    {
        var handler = GetRequiredService<ImageResizeHandler>();

        var configuration = new BlobContainerConfiguration();
        configuration.AddImageResizeHandler(options =>
        {
            options.ImageWidth = 200;
            options.ImageHeight = 200;
        });

        var originalBytes = new byte[] { 1, 2, 3, 4, 5 };
        var stream = new MemoryStream(originalBytes);

        var context = new FileHandlerContext("document.pdf", "application/pdf", stream, configuration);

        await handler.ExecuteAsync(context);

        context.BlobStream.ShouldBeSameAs(stream);
    }

    [Theory]
    [InlineData(400, 0, 800, 600, 400, 300)]   // width only, larger source: scale down to width
    [InlineData(0, 300, 800, 600, 400, 300)]   // height only, larger source: scale down to height
    [InlineData(1000, 0, 200, 150, 200, 150)]  // width only, smaller source: never upscale
    [InlineData(0, 1000, 200, 150, 200, 150)]  // height only, smaller source: never upscale
    public async Task ExecuteAsync_Should_Treat_Zero_Preset_As_Unconstrained(
        int presetWidth, int presetHeight,
        int sourceWidth, int sourceHeight,
        int expectedWidth, int expectedHeight)
    {
        var configuration = new BlobContainerConfiguration();
        configuration.AddImageResizeHandler(options =>
        {
            options.ImageWidth = presetWidth;
            options.ImageHeight = presetHeight;
        });

        var (width, height) = await ExecuteOnJpegAsync(configuration, sourceWidth, sourceHeight);

        width.ShouldBe(expectedWidth);
        height.ShouldBe(expectedHeight);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Not_Resize_When_Both_Presets_Are_Zero()
    {
        // Bypasses AddImageResizeHandler's validation, as a container configured by hand would.
        var configuration = new BlobContainerConfiguration();

        var (width, height) = await ExecuteOnJpegAsync(configuration, 800, 600);

        width.ShouldBe(800);
        height.ShouldBe(600);
    }

    [Fact]
    public void AddImageResizeHandler_Should_Reject_Configuration_Without_Any_Preset()
    {
        var configuration = new BlobContainerConfiguration();

        Should.Throw<AbpException>(() => configuration.AddImageResizeHandler(_ => { }));
    }

    [Fact]
    public void ImageWidth_And_ImageHeight_Should_Reject_Negative_Values()
    {
        var configuration = new BlobContainerConfiguration().GetImageResizeConfiguration();

        Should.Throw<ArgumentOutOfRangeException>(() => configuration.ImageWidth = -1);
        Should.Throw<ArgumentOutOfRangeException>(() => configuration.ImageHeight = -1);
    }

    private async Task<(int Width, int Height)> ExecuteOnJpegAsync(
        BlobContainerConfiguration configuration, int sourceWidth, int sourceHeight)
    {
        var handler = GetRequiredService<ImageResizeHandler>();

        var stream = new MemoryStream();
        using (var sourceImage = new Image<Rgba32>(sourceWidth, sourceHeight))
        {
            await sourceImage.SaveAsJpegAsync(stream);
        }
        stream.Position = 0;

        var context = new FileHandlerContext("photo.jpg", "image/jpeg", stream, configuration);

        await handler.ExecuteAsync(context);

        context.BlobStream.Position = 0;
        using var resultImage = await Image.LoadAsync(context.BlobStream);
        return (resultImage.Width, resultImage.Height);
    }
}
