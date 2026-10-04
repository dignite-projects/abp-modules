using System.Threading.Tasks;
using Dignite.FileExplorer.Files;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.FileExplorer.Mcp;

public class ContainerTools_Tests
{
    [Fact]
    public async Task Should_List_Only_Exposed_Containers_With_Their_Effective_Upload_Limit()
    {
        var fileAppService = Substitute.For<IFileDescriptorAppService>();
        fileAppService.GetFileContainerConfigurationAsync("site-images").Returns(new FileContainerConfigurationDto
        {
            MaxBlobSize = 1024,
            AllowedFileTypeNames = new[] { ".png", ".jpg" },
            FileCells = new[] { new FileCellDto("cover", "Cover") }
        });
        fileAppService.GetFileContainerConfigurationAsync("attachments").Returns(new FileContainerConfigurationDto
        {
            MaxBlobSize = 100 * 1024 * 1024
        });

        var options = new FileExplorerMcpOptions { MaxUploadSize = 2048 };
        options.Containers
            .Add("site-images", "Images used in site content.")
            .Add("attachments", "Downloadable attachments.");

        var containers = await new ContainerTools(fileAppService, Options.Create(options)).ListContainersAsync();

        containers.Count.ShouldBe(2);
        containers[0].Name.ShouldBe("site-images");
        containers[0].Description.ShouldBe("Images used in site content.");
        containers[0].MaxUploadSize.ShouldBe(1024);
        containers[0].AllowedFileTypes.ShouldBe(new[] { ".png", ".jpg" });
        containers[0].Cells.ShouldHaveSingleItem().Name.ShouldBe("cover");
        // The container allows 100 MB, but nothing larger than the module limit travels as base64.
        containers[1].MaxUploadSize.ShouldBe(2048);
    }

    /// <summary>
    /// One bad name in the host's list must not take the entry-point tool down for the containers that work.
    /// </summary>
    [Fact]
    public async Task Should_Leave_Out_A_Container_That_Cannot_Be_Read()
    {
        var fileAppService = Substitute.For<IFileDescriptorAppService>();
        fileAppService.GetFileContainerConfigurationAsync("site-image")
            .Returns<Task<FileContainerConfigurationDto>>(_ => throw new BusinessException(message: "The BLOB container 'site-image' is not registered."));
        fileAppService.GetFileContainerConfigurationAsync("attachments").Returns(new FileContainerConfigurationDto());

        var options = new FileExplorerMcpOptions();
        options.Containers
            .Add("site-image", "A typo for site-images.")
            .Add("attachments", "Downloadable attachments.");

        var containers = await new ContainerTools(fileAppService, Options.Create(options)).ListContainersAsync();

        containers.ShouldHaveSingleItem().Name.ShouldBe("attachments");
    }

    [Fact]
    public void Should_Refuse_A_Duplicate_Container_However_It_Is_Added()
    {
        var containers = new FileExplorerMcpOptions().Containers.Add("site-images", "Images.");

        Should.Throw<AbpException>(() => containers.Add("SITE-IMAGES", "Again."));
        Should.Throw<AbpException>(() => containers.Add(new FileExplorerMcpContainer("site-images", "Again.")));
        Should.Throw<AbpException>(() => containers.Insert(0, new FileExplorerMcpContainer("site-images", "Again.")));
        containers.Count.ShouldBe(1);
    }
}
