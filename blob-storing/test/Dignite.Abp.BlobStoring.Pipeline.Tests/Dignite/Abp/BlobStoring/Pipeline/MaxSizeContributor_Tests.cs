using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Abp.BlobStoring.Pipeline;

public class MaxSizeContributor_Tests : BlobStoringPipelineTestBase
{
    private const string ContainerName = BlobStoringPipelineTestModule.MaxSizeContainer;
    private const int Limit = (int)BlobStoringPipelineTestModule.MaxSizeInBytes;

    [Theory]
    [InlineData(Limit - 1, true)]
    [InlineData(Limit, true)]
    [InlineData(Limit - 1, false)]
    [InlineData(Limit, false)]
    public async Task Should_Save_Content_Up_To_The_Limit_Intact(int size, bool canSeek)
    {
        var content = CreateContent(size);
        var source = new TrackingStream(content, canSeek);

        await Container(ContainerName).SaveAsync("blob", source);

        (await GetStoredBytesAsync(ContainerName, "blob")).ShouldBe(content);
        (await ReadAllAsync(await Container(ContainerName).GetAsync("blob"))).ShouldBe(content);
        source.IsDisposed.ShouldBeFalse(); // the caller keeps its stream
    }

    [Fact]
    public async Task Should_Reject_A_Seekable_Stream_Over_The_Limit_Without_Reading_It()
    {
        var source = new TrackingStream(CreateContent(Limit + 1), canSeek: true);

        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(ContainerName).SaveAsync("blob", source));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);
        exception.Data["MaxSizeInBytes"].ShouldBe((long)Limit);
        source.BytesRead.ShouldBe(0);
        (await GetStoredBytesAsync(ContainerName, "blob")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Stop_Reading_A_Non_Seekable_Stream_Once_It_Passes_The_Limit()
    {
        var source = new TrackingStream(CreateContent(10 * 81920), canSeek: false);

        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(ContainerName).SaveAsync("blob", source));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);
        source.BytesRead.ShouldBeLessThanOrEqualTo(81920); // one chunk, not the whole stream
        source.IsDisposed.ShouldBeFalse();
        (await GetStoredBytesAsync(ContainerName, "blob")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Only_Count_The_Bytes_After_The_Current_Position()
    {
        var content = CreateContent(Limit + 100);
        var source = new TrackingStream(content, canSeek: true) { Position = 100 };

        await Container(ContainerName).SaveAsync("blob", source);

        (await GetStoredBytesAsync(ContainerName, "blob")).ShouldBe(content.Skip(100).ToArray());
    }

    [Fact]
    public void AddMaxSizeContributor_Should_Reject_An_Invalid_Limit()
    {
        var configuration = new BlobContainerConfiguration();

        Should.Throw<AbpException>(() => configuration.AddMaxSizeContributor(c => c.MaxSizeInBytes = 0));
        Should.Throw<AbpException>(() => configuration.AddMaxSizeContributor(c => c.MaxSizeInBytes = (long)Array.MaxLength + 1));
        configuration.PipelineContributors.ShouldBeEmpty();
    }

    [Fact]
    public void AddMaxSizeContributor_Should_Add_The_Contributor_Once_And_Keep_The_Last_Limit()
    {
        var configuration = new BlobContainerConfiguration();

        configuration.AddMaxSizeContributor(c => c.MaxSizeInBytes = 10);
        configuration.AddMaxSizeContributor(c => c.MaxSizeInBytes = 20);

        configuration.PipelineContributors.ShouldBe([typeof(MaxSizeContributor)]);
        configuration.GetMaxSizeContributorConfiguration().MaxSizeInBytes.ShouldBe(20);
    }

    private static byte[] CreateContent(int size)
    {
        var content = new byte[size];
        new Random(size).NextBytes(content);
        return content;
    }
}
