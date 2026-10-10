using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using Volo.Abp;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Abp.FileStoring.Imaging;

public class FileStorerImaging_Tests : AbpIntegratedTest<ImagingTestModule>
{
    [Fact]
    public async Task StoreAsync_Should_Resize_Through_The_Pipeline_And_Report_The_Stored_Image()
    {
        var upload = new MemoryStream();
        using (var image = new Image<Rgba32>(800, 600))
        {
            await image.SaveAsJpegAsync(upload);
        }
        upload.Position = 0;

        var stored = await GetRequiredService<IFileStorer>()
            .StoreAsync(ImagingTestModule.PhotosContainer, "photo.jpg", upload);

        var bytes = GetRequiredService<InMemoryBlobProvider>().Get(ImagingTestModule.PhotosContainer, stored.BlobName)!;
        using (var resized = Image.Load(bytes))
        {
            resized.Width.ShouldBeLessThanOrEqualTo(200);
            resized.Height.ShouldBeLessThanOrEqualTo(200);
        }

        stored.MimeType.ShouldBe("image/jpeg");
        stored.Size.ShouldBe(bytes.Length);
        stored.Hash.ShouldBe(Convert.ToHexString(SHA256.HashData(bytes)));
    }

    [Fact]
    public async Task StoreAsync_Should_Reject_A_Fake_Image_Before_Any_Handler_Decodes_It()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IFileStorer>()
            .StoreAsync(ImagingTestModule.PhotosContainer, "photo.png", new MemoryStream("not an image"u8.ToArray())));

        exception.Code.ShouldBe(FileErrorCodes.Files.ContentTypeMismatch);
    }

    [Theory]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("gif")]
    [InlineData("bmp")]
    [InlineData("webp")]
    public async Task MimeTypeDetector_Should_Detect_Every_Allowed_Image_Format(string format)
    {
        IImageEncoder encoder = format switch
        {
            "jpeg" => new JpegEncoder(),
            "png" => new PngEncoder(),
            "gif" => new GifEncoder(),
            "bmp" => new BmpEncoder(),
            "webp" => new WebpEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        var stream = new MemoryStream();
        using (var image = new Image<Rgba32>(4, 4))
        {
            await image.SaveAsync(stream, encoder);
        }
        stream.Position = 0;

        var mimeType = await GetRequiredService<IMimeTypeDetector>().DetectAsync(stream, "image");

        mimeType.ShouldBe("image/" + format);
        ImageFormatHelper.IsValidImage(mimeType, ImageFormatHelper.AllowedImageUploadFormats).ShouldBeTrue();
    }
}
