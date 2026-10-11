using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// One container with MaxSize -> AllowedContentTypes -> GZip, fed non-seekable streams.
/// </summary>
public class PipelineComposition_Tests : BlobStoringPipelineTestBase
{
    private const string ContainerName = BlobStoringPipelineTestModule.ComposedContainer;

    [Fact]
    public void Should_Run_The_Contributors_In_The_Configured_Order()
    {
        GetRequiredService<IBlobContainerConfigurationProvider>().Get(ContainerName)
            .GetEffectivePipelineContributors()
            .ShouldBe([typeof(MaxSizeContributor), typeof(AllowedContentTypesContributor), typeof(GZipContributor)]);
    }

    [Fact]
    public async Task Should_Validate_Then_Compress_A_Non_Seekable_Stream()
    {
        var content = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("plain text line\n", 500)));
        var source = new TrackingStream(content, canSeek: false);

        await Container(ContainerName).SaveAsync("notes.txt", source);

        var stored = await GetStoredBytesAsync(ContainerName, "notes.txt");
        stored.ShouldNotBeNull();
        Samples.Gunzip(stored).ShouldBe(content);
        (await ReadAllAsync(await Container(ContainerName).GetAsync("notes.txt"))).ShouldBe(content);
        source.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Reject_Too_Large_Content_Before_Inspecting_It()
    {
        var source = new TrackingStream(new byte[BlobStoringPipelineTestModule.ComposedMaxSizeInBytes + 1], canSeek: false);

        var exception = await Should.ThrowAsync<BusinessException>(() => Container(ContainerName).SaveAsync("big.txt", source));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);
        (await GetStoredBytesAsync(ContainerName, "big.txt")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Reject_A_Disallowed_Type_Before_Compressing_It()
    {
        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(ContainerName).SaveAsync("page.html", new TrackingStream(Samples.Html, canSeek: false)));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed);
        (await GetStoredBytesAsync(ContainerName, "page.html")).ShouldBeNull();
    }
}
