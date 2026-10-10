using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Testing;
using Xunit;

namespace Dignite.Abp.FileStoring;

public class FileStorer_Tests : AbpIntegratedTest<FileStoringTestModule>
{
    private const string Documents = FileStoringTestModule.DocumentsContainer;
    private const int OneMegabyte = 1024 * 1024;

    private readonly IFileStorer _fileStorer;
    private readonly FakeBlobProvider _blobProvider;

    public FileStorer_Tests()
    {
        _fileStorer = GetRequiredService<IFileStorer>();
        _blobProvider = GetRequiredService<FakeBlobProvider>();
    }

    [Fact]
    public async Task StoreAsync_Should_Store_Content_And_Report_Size_Mime_And_Hash()
    {
        var source = new TrackingStream(Samples.Png, canSeek: false);

        var stored = await _fileStorer.StoreAsync(Documents, "avatar.png", source);

        stored.MimeType.ShouldBe("image/png");
        stored.Size.ShouldBe(Samples.Png.Length);
        stored.Hash.ShouldBe(Convert.ToHexString(SHA256.HashData(Samples.Png)));
        _blobProvider.Get(Documents, stored.BlobName).ShouldBe(Samples.Png);
        source.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task StoreAsync_Should_Reject_Unregistered_Container_Before_Reading()
    {
        var source = new TrackingStream(Samples.Png, canSeek: false);

        var exception = await Should.ThrowAsync<BusinessException>(
            () => _fileStorer.StoreAsync("unregistered", "avatar.png", source));

        exception.Code.ShouldBe(FileErrorCodes.Containers.NotFound);
        source.BytesRead.ShouldBe(0);
    }

    [Fact]
    public async Task StoreAsync_Should_Stop_Reading_A_Stream_Once_It_Exceeds_The_Container_Limit()
    {
        var source = new TrackingStream(new byte[3 * OneMegabyte], canSeek: false);

        var exception = await Should.ThrowAsync<BusinessException>(
            () => _fileStorer.StoreAsync(Documents, "large.bin", source));

        exception.Code.ShouldBe(FileErrorCodes.Files.FileTooLarge);
        // Aborted within one copy chunk of the 1 MB limit, not after reading all 3 MB.
        source.BytesRead.ShouldBeLessThanOrEqualTo(OneMegabyte + 81920);
        _blobProvider.Blobs.ShouldBeEmpty();
    }

    [Fact]
    public async Task StoreAsync_Should_Reject_A_Seekable_Stream_Over_The_Limit_Without_Reading_It()
    {
        var source = new TrackingStream(new byte[OneMegabyte + 1], canSeek: true);

        var exception = await Should.ThrowAsync<BusinessException>(
            () => _fileStorer.StoreAsync(Documents, "large.bin", source));

        exception.Code.ShouldBe(FileErrorCodes.Files.FileTooLarge);
        source.BytesRead.ShouldBe(0);
    }

    [Fact]
    public async Task StoreAsync_Should_Accept_A_Stream_Exactly_At_The_Limit()
    {
        var stored = await _fileStorer.StoreAsync(
            Documents, "exact.bin", new TrackingStream(new byte[OneMegabyte], canSeek: false));

        stored.Size.ShouldBe(OneMegabyte);
        stored.MimeType.ShouldBe(MimeTypeDetector.DefaultMimeType);
    }

    [Theory]
    [InlineData(nameof(Samples.Text), "photo.png")]
    [InlineData(nameof(Samples.Executable), "photo.png")]
    [InlineData(nameof(Samples.Executable), "readme.txt")]
    public async Task StoreAsync_Should_Reject_A_Disguised_File_And_Store_Nothing(string sample, string fileName)
    {
        var exception = await Should.ThrowAsync<BusinessException>(
            () => _fileStorer.StoreAsync(Documents, fileName, new MemoryStream(Samples.Get(sample))));

        exception.Code.ShouldBe(FileErrorCodes.Files.ContentTypeMismatch);
        _blobProvider.Blobs.ShouldBeEmpty();
    }

    [Fact]
    public async Task StoreAsync_Should_Run_Handlers_In_Configured_Order_With_The_Detected_Mime_Type()
    {
        var stored = await _fileStorer.StoreAsync(
            FileStoringTestModule.PipelineContainer,
            "notes.txt",
            new MemoryStream(Encoding.UTF8.GetBytes("hello")));

        GetRequiredService<HandlerLog>().Entries.ToArray()
            .ShouldBe(["first:text/plain", "uppercase", "second:HELLO"]);

        // What is reported and stored is the transformed content, not the upload.
        var expected = Encoding.UTF8.GetBytes("HELLO");
        _blobProvider.Get(FileStoringTestModule.PipelineContainer, stored.BlobName).ShouldBe(expected);
        stored.Size.ShouldBe(expected.Length);
        stored.Hash.ShouldBe(Convert.ToHexString(SHA256.HashData(expected)));
        stored.MimeType.ShouldBe("text/plain");
    }

    [Fact]
    public async Task StoreAsync_Should_Delete_The_Blob_When_The_Save_Fails_After_Writing()
    {
        _blobProvider.AfterWrite = _ => throw new IOException("provider failed mid-save");

        await Should.ThrowAsync<IOException>(
            () => _fileStorer.StoreAsync(Documents, "avatar.png", new MemoryStream(Samples.Png)));

        _blobProvider.Blobs.ShouldBeEmpty();
        _blobProvider.DeletedBlobNames.Count.ShouldBe(1);
    }

    [Fact]
    public async Task StoreAsync_Should_Not_Delete_An_Existing_Blob_On_A_Name_Collision()
    {
        var original = Encoding.UTF8.GetBytes("someone else's file");
        _blobProvider.Put(FileStoringTestModule.FixedNameContainer, FixedBlobNameGenerator.BlobName, original);

        await Should.ThrowAsync<BlobAlreadyExistsException>(
            () => _fileStorer.StoreAsync(
                FileStoringTestModule.FixedNameContainer, "notes.txt", new MemoryStream(Samples.Text)));

        _blobProvider.Get(FileStoringTestModule.FixedNameContainer, FixedBlobNameGenerator.BlobName)
            .ShouldBe(original);
        _blobProvider.DeletedBlobNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task StoreAsync_Should_Honor_A_Cancelled_Token_Before_Reading()
    {
        var source = new TrackingStream(Samples.Png, canSeek: false);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _fileStorer.StoreAsync(Documents, "avatar.png", source, cancellation.Token));

        source.BytesRead.ShouldBe(0);
        _blobProvider.Blobs.ShouldBeEmpty();
    }

    [Fact]
    public async Task StoreAsync_Should_Still_Compensate_When_Cancelled_During_The_Save()
    {
        using var cancellation = new CancellationTokenSource();
        _blobProvider.AfterWrite = async args =>
        {
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, args.CancellationToken);
        };

        await Should.ThrowAsync<OperationCanceledException>(
            () => _fileStorer.StoreAsync(Documents, "avatar.png", new MemoryStream(Samples.Png), cancellation.Token));

        // The compensating delete runs on its own token, so the partial blob is gone even though
        // the request token was already cancelled.
        _blobProvider.Blobs.ShouldBeEmpty();
        _blobProvider.DeletedBlobNames.Count.ShouldBe(1);
    }

    [Fact]
    public async Task StoreAsync_Should_Use_The_Container_Blob_Name_Generator()
    {
        var stored = await _fileStorer.StoreAsync(
            FileStoringTestModule.FixedNameContainer, "notes.txt", new MemoryStream(Samples.Text));

        stored.BlobName.ShouldBe(FixedBlobNameGenerator.BlobName);
    }

    [Fact]
    public async Task DeleteAsync_Should_Delete_The_Blob()
    {
        var stored = await _fileStorer.StoreAsync(Documents, "avatar.png", new MemoryStream(Samples.Png));

        (await _fileStorer.DeleteAsync(Documents, stored.BlobName)).ShouldBeTrue();
        (await _fileStorer.DeleteAsync(Documents, stored.BlobName)).ShouldBeFalse();
        _blobProvider.Blobs.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_Should_Reject_Unregistered_Container()
    {
        var exception = await Should.ThrowAsync<BusinessException>(
            () => _fileStorer.DeleteAsync("unregistered", "anything"));

        exception.Code.ShouldBe(FileErrorCodes.Containers.NotFound);
    }
}
