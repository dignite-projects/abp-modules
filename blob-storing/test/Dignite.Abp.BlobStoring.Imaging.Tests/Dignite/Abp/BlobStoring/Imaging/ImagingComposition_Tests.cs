using System.Text;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Shouldly;
using SkiaSharp;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// One container with MaxSize -> AllowedContentTypes(image/*) -> Resize -> Compress, fed non-seekable streams.
/// </summary>
public class ImagingComposition_Tests : BlobStoringImagingTestBase
{
    private const string ContainerName = BlobStoringImagingTestModule.ComposedContainer;

    [Fact]
    public void Should_Run_The_Contributors_In_The_Configured_Order()
    {
        GetRequiredService<IBlobContainerConfigurationProvider>().Get(ContainerName)
            .GetEffectivePipelineContributors()
            .ShouldBe([
                typeof(MaxSizeContributor),
                typeof(AllowedContentTypesContributor),
                typeof(ImageResizeContributor),
                typeof(ImageCompressContributor)
            ]);
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "upload.jpg")]
    [InlineData(SKEncodedImageFormat.Png, "upload.png")]
    public async Task Should_Validate_Resize_And_Compress_A_Non_Seekable_Upload(SKEncodedImageFormat format, string blobName)
    {
        var content = TestImages.Noise(1024, 768, format, quality: 95);
        var source = new TrackingStream(content, canSeek: false);

        await Container(ContainerName).SaveAsync(blobName, source);

        var read = await ReadAllAsync(await Container(ContainerName).GetAsync(blobName));
        TestImages.GetDimensions(read).ShouldBe((BlobStoringImagingTestModule.ComposedMaxSize, 192));
        TestImages.GetFormat(read).ShouldBe(format);
        read.Length.ShouldBeLessThan(content.Length);
        source.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Reject_Content_That_Is_Not_An_Image_Before_The_Image_Contributors()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => Container(ContainerName).SaveAsync(
            "notes.txt",
            new TrackingStream(Encoding.UTF8.GetBytes("not an image"), canSeek: false)));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed);
        (await GetStoredBytesAsync(ContainerName, "notes.txt")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Reject_Too_Large_Content_Before_Decoding_It()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => Container(ContainerName).SaveAsync(
            "huge.png",
            new TrackingStream(new byte[BlobStoringImagingTestModule.ComposedMaxSizeInBytes + 1], canSeek: false)));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);
    }
}
