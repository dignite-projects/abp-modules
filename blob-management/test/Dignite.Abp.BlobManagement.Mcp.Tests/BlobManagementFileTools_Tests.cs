using System;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Directories;
using Dignite.Abp.BlobManagement.Files;
using NSubstitute;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace Dignite.Abp.BlobManagement.Mcp;

/// <summary>
/// What the tools add to the services they call: a file listing within its paging limits, and each question asked of
/// the place it names.
/// </summary>
public class BlobManagementFileTools_Tests
{
    private readonly IBlobManagementFileAppService _files = Substitute.For<IBlobManagementFileAppService>();
    private readonly IBlobManagementDirectoryAppService _directories = Substitute.For<IBlobManagementDirectoryAppService>();

    [Fact]
    public async Task Listing_Files_Should_Keep_To_Its_Paging_Limits()
    {
        var directoryId = Guid.NewGuid();
        var tools = new BlobManagementFileTools(_files);

        await tools.ListFilesAsync("documents", directoryId, filter: "report", skipCount: -5, maxResultCount: 1000);
        await _files.Received(1).GetListAsync(Arg.Is<GetBlobManagementFilesInput>(input =>
            input.ContainerName == "documents" &&
            input.DirectoryId == directoryId &&
            input.Filter == "report" &&
            input.SkipCount == 0 &&
            input.MaxResultCount == 100));

        await tools.ListFilesAsync("documents", maxResultCount: 0);
        await _files.Received(1).GetListAsync(Arg.Is<GetBlobManagementFilesInput>(input =>
            input.DirectoryId == null && input.MaxResultCount == 1));
    }

    [Fact]
    public async Task A_File_Should_Be_Read_By_Its_Id()
    {
        var id = Guid.NewGuid();
        _files.GetAsync(id).Returns(new BlobManagementFileDto { Name = "report.pdf" });

        (await new BlobManagementFileTools(_files).GetFileAsync(id)).Name.ShouldBe("report.pdf");
    }

    [Fact]
    public async Task Directories_Should_Be_Listed_Where_Asked()
    {
        var parentId = Guid.NewGuid();
        _directories.GetListAsync(Arg.Any<GetBlobManagementDirectoriesInput>())
            .Returns(new ListResultDto<BlobManagementDirectoryDto>());

        await new BlobManagementDirectoryTools(_directories).ListDirectoriesAsync("documents", parentId);

        await _directories.Received(1).GetListAsync(Arg.Is<GetBlobManagementDirectoriesInput>(input =>
            input.ContainerName == "documents" && input.ParentId == parentId));
    }
}
