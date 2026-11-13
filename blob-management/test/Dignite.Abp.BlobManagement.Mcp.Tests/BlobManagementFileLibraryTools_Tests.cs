using System;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Containers;
using Dignite.Abp.BlobManagement.Directories;
using Dignite.Abp.BlobManagement.Files;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.Validation;
using Xunit;

namespace Dignite.Abp.BlobManagement.Mcp;

/// <summary>
/// What the listing, renaming and moving tools add to the services: a listing within its paging limits, a rename or
/// move that changes only what was passed and, refused, leaves nothing half done, and an upload that carries the
/// directory it is filed in.
/// </summary>
public class BlobManagementFileLibraryTools_Tests
{
    private readonly IBlobManagementFileAppService _files = Substitute.For<IBlobManagementFileAppService>();
    private readonly IBlobManagementContainerAppService _containers = Substitute.For<IBlobManagementContainerAppService>();
    private readonly IBlobManagementDirectoryAppService _directories = Substitute.For<IBlobManagementDirectoryAppService>();

    [Fact]
    public async Task Listing_Should_Keep_To_Its_Paging_Limits()
    {
        var directoryId = Guid.NewGuid();
        await Tools().ListFilesAsync("documents", directoryId, skipCount: -5, maxResultCount: 1000);

        await _files.Received(1).GetListAsync(Arg.Is<GetBlobManagementFilesInput>(input =>
            input.ContainerName == "documents" && input.DirectoryId == directoryId && input.SkipCount == 0 && input.MaxResultCount == 100));
    }

    [Fact]
    public async Task Updating_Should_Change_Only_What_Was_Passed()
    {
        var id = Guid.NewGuid();
        _files.UpdateAsync(id, Arg.Any<UpdateBlobManagementFileInput>()).Returns(new BlobManagementFileDto { Name = "renamed.pdf" });
        _files.MoveAsync(id, Arg.Any<MoveBlobManagementFileInput>()).Returns(new BlobManagementFileDto { Name = "renamed.pdf" });

        await Tools().UpdateFileAsync(id, name: "renamed.pdf");
        await _files.Received(1).UpdateAsync(id, Arg.Is<UpdateBlobManagementFileInput>(input => input.Name == "renamed.pdf"));
        await _files.DidNotReceive().MoveAsync(Arg.Any<Guid>(), Arg.Any<MoveBlobManagementFileInput>());

        var directoryId = Guid.NewGuid();
        await Tools().UpdateFileAsync(id, directoryId: directoryId);
        await _files.Received(1).MoveAsync(id, Arg.Is<MoveBlobManagementFileInput>(input => input.DirectoryId == directoryId));

        await Tools().UpdateFileAsync(id, moveToTop: true);
        await _files.Received(1).MoveAsync(id, Arg.Is<MoveBlobManagementFileInput>(input => input.DirectoryId == null));

        await Should.ThrowAsync<AbpValidationException>(() => Tools().UpdateFileAsync(id, directoryId: Guid.NewGuid(), moveToTop: true));
        await Should.ThrowAsync<AbpValidationException>(() => Tools().UpdateFileAsync(id));
    }

    [Fact]
    public async Task A_Refused_Move_Should_Not_Leave_The_File_Renamed()
    {
        var id = Guid.NewGuid();
        _files.MoveAsync(id, Arg.Any<MoveBlobManagementFileInput>())
            .Returns<Task<BlobManagementFileDto>>(_ => throw new InvalidOperationException("refused"));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            Tools().UpdateFileAsync(id, name: "renamed.pdf", directoryId: Guid.NewGuid()));
        await _files.DidNotReceive().UpdateAsync(Arg.Any<Guid>(), Arg.Any<UpdateBlobManagementFileInput>());

        // A name the rename would refuse is refused before the move.
        await Should.ThrowAsync<AbpValidationException>(() => Tools().UpdateFileAsync(id, name: "  ", moveToTop: true));
        await Should.ThrowAsync<AbpValidationException>(() =>
            Tools().UpdateFileAsync(id, name: new string('a', FileDescriptorConsts.MaxNameLength + 1), moveToTop: true));
        await _files.Received(1).MoveAsync(Arg.Any<Guid>(), Arg.Any<MoveBlobManagementFileInput>());
    }

    [Fact]
    public async Task Directories_Should_Be_Listed_And_Created_Where_Asked()
    {
        var parentId = Guid.NewGuid();
        _directories.GetListAsync(Arg.Any<GetBlobManagementDirectoriesInput>())
            .Returns(new Volo.Abp.Application.Dtos.ListResultDto<BlobManagementDirectoryDto>());
        var tools = new BlobManagementDirectoryTools(_directories);

        await tools.ListDirectoriesAsync("documents", parentId);
        await _directories.Received(1).GetListAsync(Arg.Is<GetBlobManagementDirectoriesInput>(input =>
            input.ContainerName == "documents" && input.ParentId == parentId));

        await tools.CreateDirectoryAsync("documents", "Reports", parentId);
        await _directories.Received(1).CreateAsync(Arg.Is<CreateBlobManagementDirectoryInput>(input =>
            input.ContainerName == "documents" && input.Name == "Reports" && input.ParentId == parentId));
    }

    private BlobManagementFileTools Tools()
    {
        return new BlobManagementFileTools(_files, _containers, Options.Create(new BlobManagementMcpOptions()));
    }
}
