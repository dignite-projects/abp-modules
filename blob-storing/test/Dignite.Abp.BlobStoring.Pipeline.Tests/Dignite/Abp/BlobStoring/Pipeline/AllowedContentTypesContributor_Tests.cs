using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Abp.BlobStoring.Pipeline;

public class AllowedContentTypesContributor_Tests : BlobStoringPipelineTestBase
{
    [Theory]
    [InlineData(BlobStoringPipelineTestModule.ImagesContainer, nameof(Samples.Png), "photo.png")]
    [InlineData(BlobStoringPipelineTestModule.ImagesContainer, nameof(Samples.Jpeg), "photo.jpg")] // listed as "IMAGE/JPEG"
    [InlineData(BlobStoringPipelineTestModule.ImageWildcardContainer, nameof(Samples.Png), "photo.png")]
    [InlineData(BlobStoringPipelineTestModule.ImageWildcardContainer, nameof(Samples.Jpeg), "photo")]
    [InlineData(BlobStoringPipelineTestModule.UnidentifiedAllowedContainer, nameof(Samples.UnknownBinary), "data.bin")]
    [InlineData(BlobStoringPipelineTestModule.WordDocumentsContainer, nameof(Samples.WordDocument), "report.docx")]
    [InlineData(BlobStoringPipelineTestModule.PlainTextContainer, nameof(Samples.Text), "notes.txt")]
    public async Task Should_Save_Allowed_Content_Intact(string containerName, string sample, string blobName)
    {
        foreach (var canSeek in new[] { true, false })
        {
            var content = Samples.Get(sample);
            var source = new TrackingStream(content, canSeek);

            await Container(containerName).SaveAsync(blobName, source, overrideExisting: true);

            (await GetStoredBytesAsync(containerName, blobName)).ShouldBe(content);
            source.IsDisposed.ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData(BlobStoringPipelineTestModule.ImagesContainer, nameof(Samples.Executable), "setup.exe", "application/x-msdownload")]
    [InlineData(BlobStoringPipelineTestModule.ImagesContainer, nameof(Samples.Executable), "setup", "application/x-msdownload")]
    [InlineData(BlobStoringPipelineTestModule.ImageWildcardContainer, nameof(Samples.Executable), "setup", "application/x-msdownload")]
    [InlineData(BlobStoringPipelineTestModule.ImagesContainer, nameof(Samples.UnknownBinary), "data.bin", MimeTypeDetector.DefaultMimeType)]
    [InlineData(BlobStoringPipelineTestModule.ImageWildcardContainer, nameof(Samples.UnknownBinary), "data", MimeTypeDetector.DefaultMimeType)]
    [InlineData(BlobStoringPipelineTestModule.WordDocumentsContainer, nameof(Samples.PlainZip), "archive.zip", "application/zip")]
    [InlineData(BlobStoringPipelineTestModule.PlainTextContainer, nameof(Samples.Html), "notes.txt", "text/html")]
    [InlineData(BlobStoringPipelineTestModule.PlainTextContainer, nameof(Samples.Script), "notes.txt", "text/html")]
    [InlineData(BlobStoringPipelineTestModule.PlainTextContainer, nameof(Samples.Svg), "notes.txt", "image/svg+xml")]
    public async Task Should_Reject_Content_Of_Another_Type(string containerName, string sample, string blobName, string detected)
    {
        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(containerName).SaveAsync(blobName, new TrackingStream(Samples.Get(sample), canSeek: false)));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed);
        exception.Data["ContentType"].ShouldBe(detected);
        (await GetStoredBytesAsync(containerName, blobName)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Reject_Content_That_Contradicts_The_Blob_Name()
    {
        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(BlobStoringPipelineTestModule.ImagesContainer).SaveAsync("x.pdf", new TrackingStream(Samples.Png, canSeek: true)));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeMismatch);
        (await GetStoredBytesAsync(BlobStoringPipelineTestModule.ImagesContainer, "x.pdf")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Cap_Buffering_Of_A_Non_Seekable_Stream_Without_A_MaxSize_Contributor()
    {
        var previous = BlobStoringPipelineConsts.DefaultMaxBufferedBytes;
        BlobStoringPipelineConsts.DefaultMaxBufferedBytes = 64;
        try
        {
            var source = new TrackingStream(new byte[100_000], canSeek: false);

            var exception = await Should.ThrowAsync<BusinessException>(
                () => Container(BlobStoringPipelineTestModule.ImagesContainer).SaveAsync("big.png", source));

            exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);
            source.BytesRead.ShouldBeLessThan(100_000);
        }
        finally
        {
            BlobStoringPipelineConsts.DefaultMaxBufferedBytes = previous;
        }
    }

    [Fact]
    public void AddAllowedContentTypesContributor_Should_Reject_An_Invalid_List()
    {
        var configuration = new BlobContainerConfiguration();

        Should.Throw<AbpException>(() => configuration.AddAllowedContentTypesContributor(_ => { }));
        Should.Throw<AbpException>(() => configuration.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = []));
        Should.Throw<AbpException>(() => configuration.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["png"]));
        Should.Throw<AbpException>(() => configuration.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["*/*"]));
        Should.Throw<AbpException>(() => configuration.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["image/ png"]));
        configuration.PipelineContributors.ShouldBeEmpty();
    }
}
