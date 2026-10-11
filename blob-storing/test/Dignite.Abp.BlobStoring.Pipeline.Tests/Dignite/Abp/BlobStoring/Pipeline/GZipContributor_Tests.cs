using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Abp.BlobStoring.Pipeline;

public class GZipContributor_Tests : BlobStoringPipelineTestBase
{
    private static readonly byte[] Content =
        Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("compressible content, ", 2000)));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_Store_Gzip_And_Return_The_Original_Content(bool canSeek)
    {
        const string containerName = BlobStoringPipelineTestModule.GZipContainer;
        var source = new TrackingStream(Content, canSeek);

        await Container(containerName).SaveAsync("text", source);

        var stored = await GetStoredBytesAsync(containerName, "text");
        stored.ShouldNotBeNull();
        stored.Take(2).ShouldBe(new byte[] { 0x1F, 0x8B });
        stored.Length.ShouldBeLessThan(Content.Length);
        Samples.Gunzip(stored).ShouldBe(Content);

        (await ReadAllAsync(await Container(containerName).GetAsync("text"))).ShouldBe(Content);
        source.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Compress_Before_The_Encryption()
    {
        const string containerName = BlobStoringPipelineTestModule.GZipEncryptedContainer;

        await Container(containerName).SaveAsync("text", new TrackingStream(Content, canSeek: false));

        var stored = await GetStoredBytesAsync(containerName, "text");
        stored.ShouldNotBeNull();
        stored.Take(2).ShouldNotBe(new byte[] { 0x1F, 0x8B }); // ciphertext is stored...
        stored.Length.ShouldBeLessThan(Content.Length / 10);    // ...of the compressed content

        (await ReadAllAsync(await Container(containerName).GetAsync("text"))).ShouldBe(Content);
    }

    [Fact]
    public async Task Should_Compress_Content_Within_A_Configured_MaxSize()
    {
        const string containerName = BlobStoringPipelineTestModule.GZipMaxSizeContainer;
        var content = new byte[BlobStoringPipelineTestModule.MaxSizeInBytes]; // compresses far below the limit

        await Container(containerName).SaveAsync("blob", new TrackingStream(content, canSeek: false));

        Samples.Gunzip((await GetStoredBytesAsync(containerName, "blob"))!).ShouldBe(content);
    }

    [Fact]
    public async Task Should_Stop_Reading_A_Non_Seekable_Stream_Past_A_Configured_MaxSize()
    {
        const string containerName = BlobStoringPipelineTestModule.GZipMaxSizeContainer;
        var source = new TrackingStream(new byte[10 * 81920], canSeek: false);

        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(containerName).SaveAsync("blob", source));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);
        exception.Data["MaxSizeInBytes"].ShouldBe(BlobStoringPipelineTestModule.MaxSizeInBytes);
        source.BytesRead.ShouldBeLessThanOrEqualTo(81920); // one chunk, not the whole stream
        source.IsDisposed.ShouldBeFalse();
        (await GetStoredBytesAsync(containerName, "blob")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Reject_A_Seekable_Stream_Over_A_Configured_MaxSize_Without_Reading_It()
    {
        const string containerName = BlobStoringPipelineTestModule.GZipMaxSizeContainer;
        var source = new TrackingStream(new byte[BlobStoringPipelineTestModule.MaxSizeInBytes + 1], canSeek: true);

        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(containerName).SaveAsync("blob", source));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);
        source.BytesRead.ShouldBe(0);
        (await GetStoredBytesAsync(containerName, "blob")).ShouldBeNull();
    }

    [Fact]
    public void AddGZipContributor_Should_Default_To_Optimal_And_Reject_An_Unknown_Level()
    {
        var configuration = new BlobContainerConfiguration();

        Should.Throw<AbpException>(() => configuration.AddGZipContributor(c => c.CompressionLevel = (CompressionLevel)42));
        configuration.PipelineContributors.ShouldBeEmpty();

        configuration = new BlobContainerConfiguration();
        configuration.AddGZipContributor();
        configuration.GetGZipContributorConfiguration().CompressionLevel.ShouldBe(CompressionLevel.Optimal);
        configuration.PipelineContributors.ShouldBe([typeof(GZipContributor)]);
    }
}
