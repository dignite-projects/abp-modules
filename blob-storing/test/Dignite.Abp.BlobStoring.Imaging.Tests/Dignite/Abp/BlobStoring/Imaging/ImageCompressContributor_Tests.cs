using System.Text;
using System.Threading.Tasks;
using Shouldly;
using SkiaSharp;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Abp.BlobStoring.Imaging;

public class ImageCompressContributor_Tests : BlobStoringImagingTestBase
{
    private const string ContainerName = BlobStoringImagingTestModule.CompressContainer;

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "photo.jpg")]
    [InlineData(SKEncodedImageFormat.Webp, "photo.webp")]
    public async Task Should_Store_A_Smaller_Decodable_Image_Of_The_Same_Size_And_Format(SKEncodedImageFormat format, string blobName)
    {
        // Encoded at full quality, so SkiaSharp's default quality (75) shrinks it.
        var content = TestImages.Noise(320, 240, format, quality: 100);
        var source = new TrackingStream(content, canSeek: false);

        await Container(ContainerName).SaveAsync(blobName, source);

        var stored = await GetStoredBytesAsync(ContainerName, blobName);
        stored.ShouldNotBeNull();
        stored.Length.ShouldBeLessThan(content.Length);
        TestImages.GetDimensions(stored).ShouldBe((320, 240));
        TestImages.GetFormat(stored).ShouldBe(format);
        (await ReadAllAsync(await Container(ContainerName).GetAsync(blobName))).ShouldBe(stored);
        source.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Keep_An_Image_The_Provider_Cannot_Shrink()
    {
        // Re-encoding random pixels as PNG does not make them smaller: ABP's compressors report Canceled, and the
        // original is stored rather than the save failing.
        var content = TestImages.Noise(64, 64, SKEncodedImageFormat.Png);

        await Container(ContainerName).SaveAsync("noise.png", content);

        (await GetStoredBytesAsync(ContainerName, "noise.png")).ShouldBe(content);
    }

    [Fact]
    public async Task Should_Pass_Content_That_Is_Not_An_Image_Through_Byte_For_Byte()
    {
        var content = Encoding.UTF8.GetBytes("plain text is not compressed by the image contributor");

        await Container(ContainerName).SaveAsync("notes.txt", content);

        (await GetStoredBytesAsync(ContainerName, "notes.txt")).ShouldBe(content);
    }

    [Fact]
    public void AddImageCompressContributor_Should_Add_The_Contributor_Once()
    {
        var configuration = new BlobContainerConfiguration();

        configuration.AddImageCompressContributor();
        configuration.AddImageCompressContributor(_ => { });

        configuration.PipelineContributors.ShouldBe([typeof(ImageCompressContributor)]);
    }
}
