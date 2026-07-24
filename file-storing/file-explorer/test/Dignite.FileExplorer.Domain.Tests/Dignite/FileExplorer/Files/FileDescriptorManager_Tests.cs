using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Xunit;
using Xunit.Sdk;

namespace Dignite.FileExplorer.Files;

public class FileDescriptorManager_Tests : FileExplorerDomainTestBase
{
    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly IBlobProvider _blobProvider;

    public FileDescriptorManager_Tests()
    {
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _blobProvider = GetRequiredService<IBlobProvider>();
    }

    [Fact]
    public async Task CreateAsync_ShouldWorkProperly_WithCorrectData()
    {
        var memoryStream = new MemoryStream();
        await memoryStream.WriteAsync(Encoding.UTF8.GetBytes("text content"));
        memoryStream.Position = 0;

        var stream = new RemoteStreamContent(memoryStream, "text.txt", "text/plain");

        var files = await _fileDescriptorManager.CreateAsync<DefaultContainer>(
            stream,
            null,
            null,
            new FakeEntity(Guid.NewGuid())
            );

        files.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldNotDeleteBlob_WhenAnotherDescriptorStillReferencesIt()
    {
        var owner = await CreateFileAsync("shared content - still referenced");
        var referrer = await CreateFileAsync("shared content - still referenced");

        referrer.ReferBlobName.ShouldBe(owner.BlobName);

        // FileDescriptorAppService.DeleteAsync runs the whole delete inside one ambient
        // UnitOfWork (ABP wraps ApplicationService methods automatically) - nothing is
        // actually committed to the database until it completes. Reproduce that boundary
        // here instead of calling the manager bare, or every repository call gets its own
        // implicit UnitOfWork and commits immediately, hiding ordering bugs.
        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(owner));

        await _blobProvider.DidNotReceive().DeleteAsync(
            Arg.Is<BlobProviderDeleteArgs>(a => a.BlobName == owner.BlobName));
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteBlob_WhenLastReferencingDescriptorIsDeleted()
    {
        // Regression test: ReferencingAnyAsync must not see the descriptor being deleted
        // as a reference to itself, or the physical blob is never cleaned up.
        var owner = await CreateFileAsync("shared content - last reference");
        var referrer = await CreateFileAsync("shared content - last reference");

        referrer.ReferBlobName.ShouldBe(owner.BlobName);

        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(owner));
        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(referrer));

        await _blobProvider.Received(1).DeleteAsync(
            Arg.Is<BlobProviderDeleteArgs>(a => a.BlobName == owner.BlobName));
    }

    private async Task<FileDescriptor> CreateFileAsync(string content)
    {
        var memoryStream = new MemoryStream();
        await memoryStream.WriteAsync(Encoding.UTF8.GetBytes(content));
        memoryStream.Position = 0;

        var stream = new RemoteStreamContent(memoryStream, "text.txt", "text/plain");

        return await _fileDescriptorManager.CreateAsync<DefaultContainer>(
            stream,
            null,
            null,
            new FakeEntity(Guid.NewGuid())
            );
    }
}
