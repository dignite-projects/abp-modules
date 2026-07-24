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
}
