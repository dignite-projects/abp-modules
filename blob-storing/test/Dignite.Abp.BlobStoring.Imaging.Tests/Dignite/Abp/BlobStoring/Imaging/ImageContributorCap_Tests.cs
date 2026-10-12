using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Shouldly;
using SkiaSharp;
using Volo.Abp;
using Xunit;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The image contributors read the content, so without a MaxSize contributor they are capped at
/// <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/> even for a seekable stream. These tests lower that
/// static default, so they must not run alongside any other test.
/// </summary>
[Collection(NonParallelCollection.Name)]
public class ImageContributorCap_Tests : BlobStoringImagingTestBase
{
    private const long LowDefault = 1024;

    [Theory]
    [InlineData(BlobStoringImagingTestModule.ResizeContainer, true)]
    [InlineData(BlobStoringImagingTestModule.ResizeContainer, false)]
    [InlineData(BlobStoringImagingTestModule.CompressContainer, true)]
    [InlineData(BlobStoringImagingTestModule.CompressContainer, false)]
    public async Task Should_Reject_An_Image_Over_The_Default_Cap_Without_A_MaxSize_Contributor(string containerName, bool canSeek)
    {
        var content = TestImages.Noise(128, 128, SKEncodedImageFormat.Png);
        content.Length.ShouldBeGreaterThan((int)LowDefault);

        var previous = BlobStoringPipelineConsts.DefaultMaxBufferedBytes;
        BlobStoringPipelineConsts.DefaultMaxBufferedBytes = LowDefault;
        try
        {
            var source = new TrackingStream(content, canSeek);

            var exception = await Should.ThrowAsync<BusinessException>(
                () => Container(containerName).SaveAsync("photo.png", source));

            exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);
            exception.Data["MaxSizeInBytes"].ShouldBe(LowDefault);
            source.IsDisposed.ShouldBeFalse();
            (await GetStoredBytesAsync(containerName, "photo.png")).ShouldBeNull();
        }
        finally
        {
            BlobStoringPipelineConsts.DefaultMaxBufferedBytes = previous;
        }
    }

    [Fact]
    public async Task Should_Still_Process_A_Seekable_Image_Within_The_Default_Cap()
    {
        const string containerName = BlobStoringImagingTestModule.ResizeContainer;
        var content = TestImages.Noise(128, 128, SKEncodedImageFormat.Png);

        var previous = BlobStoringPipelineConsts.DefaultMaxBufferedBytes;
        BlobStoringPipelineConsts.DefaultMaxBufferedBytes = content.Length;
        try
        {
            await Container(containerName).SaveAsync("photo.png", new TrackingStream(content, canSeek: true));

            var stored = await GetStoredBytesAsync(containerName, "photo.png");
            stored.ShouldNotBeNull();
            TestImages.GetDimensions(stored).ShouldBe((128, 128)); // already inside the 200x200 box
        }
        finally
        {
            BlobStoringPipelineConsts.DefaultMaxBufferedBytes = previous;
        }
    }
}

/// <summary>
/// Tests that mutate <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/> run in this collection, which
/// xunit does not run in parallel with any other.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class NonParallelCollection
{
    public const string Name = "NonParallel";
}
