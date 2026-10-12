using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// Every content-reading contributor is capped at the container's MaxSize or, without one,
/// <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/>. These tests lower that static default, so they
/// must not run alongside any other test.
/// </summary>
[Collection(NonParallelCollection.Name)]
public class BlobStreamBuffering_Tests : BlobStoringPipelineTestBase
{
    private const long LowDefault = 64;
    private const int OverCap = 100_000;

    [Fact]
    public void GetMaxBufferedBytes_Should_Use_The_MaxSize_When_Configured_Else_The_Default()
    {
        var configuration = new BlobContainerConfiguration();
        BlobStreamBuffering.GetMaxBufferedBytes(configuration).ShouldBe(BlobStoringPipelineConsts.DefaultMaxBufferedBytes);

        configuration.AddMaxSizeContributor(c => c.MaxSizeInBytes = 10);
        BlobStreamBuffering.GetMaxBufferedBytes(configuration).ShouldBe(10);
    }

    [Fact]
    public async Task AllowedContentTypes_Should_Cap_Buffering_Of_A_Non_Seekable_Stream_Without_A_MaxSize_Contributor()
    {
        await WithLowDefaultAsync(async () =>
        {
            var source = new TrackingStream(new byte[OverCap], canSeek: false);

            var exception = await Should.ThrowAsync<BusinessException>(
                () => Container(BlobStoringPipelineTestModule.ImagesContainer).SaveAsync("big.png", source));

            exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);
            source.BytesRead.ShouldBeLessThan(OverCap);
        });
    }

    [Fact]
    public async Task AllowedContentTypes_Should_Reject_A_Seekable_Stream_Over_The_Default_Cap_Without_Reading_It()
    {
        await WithLowDefaultAsync(async () =>
        {
            var source = new TrackingStream(Samples.Png, canSeek: true); // 96 bytes, a valid PNG header

            var exception = await Should.ThrowAsync<BusinessException>(
                () => Container(BlobStoringPipelineTestModule.ImagesContainer).SaveAsync("photo.png", source));

            exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);
            exception.Data["MaxSizeInBytes"].ShouldBe(LowDefault);
            source.BytesRead.ShouldBe(0);
            (await GetStoredBytesAsync(BlobStoringPipelineTestModule.ImagesContainer, "photo.png")).ShouldBeNull();
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GZip_Should_Reject_Input_Over_The_Default_Cap_Without_A_MaxSize_Contributor(bool canSeek)
    {
        await WithLowDefaultAsync(async () =>
        {
            var source = new TrackingStream(new byte[OverCap], canSeek);

            var exception = await Should.ThrowAsync<BusinessException>(
                () => Container(BlobStoringPipelineTestModule.GZipContainer).SaveAsync("blob", source));

            exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);
            exception.Data["MaxSizeInBytes"].ShouldBe(LowDefault);
            source.BytesRead.ShouldBeLessThan(OverCap);
            source.IsDisposed.ShouldBeFalse();
            (await GetStoredBytesAsync(BlobStoringPipelineTestModule.GZipContainer, "blob")).ShouldBeNull();
        });
    }

    [Fact]
    public async Task GZip_Should_Still_Compress_Input_Within_The_Default_Cap()
    {
        await WithLowDefaultAsync(async () =>
        {
            var content = new byte[LowDefault];

            await Container(BlobStoringPipelineTestModule.GZipContainer).SaveAsync("blob", new TrackingStream(content, canSeek: false));

            Samples.Gunzip((await GetStoredBytesAsync(BlobStoringPipelineTestModule.GZipContainer, "blob"))!).ShouldBe(content);
        });
    }

    private static async Task WithLowDefaultAsync(Func<Task> action)
    {
        var previous = BlobStoringPipelineConsts.DefaultMaxBufferedBytes;
        BlobStoringPipelineConsts.DefaultMaxBufferedBytes = LowDefault;
        try
        {
            await action();
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
