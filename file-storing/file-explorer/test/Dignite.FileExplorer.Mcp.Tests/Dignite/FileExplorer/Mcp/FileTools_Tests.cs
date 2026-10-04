using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dignite.FileExplorer.Files;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Validation;
using Xunit;

namespace Dignite.FileExplorer.Mcp;

public class FileTools_Tests
{
    private const string Container = "site-images";

    private readonly IFileDescriptorAppService _fileAppService = Substitute.For<IFileDescriptorAppService>();
    private readonly FileExplorerMcpOptions _options = new();
    private readonly FileTools _tools;
    private CreateFileInput? _created;
    private byte[]? _createdBytes;

    public FileTools_Tests()
    {
        _options.Containers.Add(Container, "Images used in site content.");
        _options.MaxUploadSize = 16;

        _fileAppService.GetFileContainerConfigurationAsync(Arg.Any<string>())
            .Returns(new FileContainerConfigurationDto { MaxBlobSize = 0 });
        _fileAppService.CreateAsync(Arg.Do<CreateFileInput>(input =>
            {
                _created = input;
                using var memory = new MemoryStream();
                input.File.GetStream().CopyTo(memory);
                _createdBytes = memory.ToArray();
            }))
            .Returns(callInfo => new FileDescriptorDto
            {
                ContainerName = callInfo.Arg<CreateFileInput>().ContainerName,
                BlobName = "2026/cover.png",
                Name = callInfo.Arg<CreateFileInput>().File.FileName
            });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.test");

        var options = Options.Create(_options);
        _tools = new FileTools(
            _fileAppService,
            new FileExplorerMcpContainerGuard(options),
            new FileExplorerMcpFileUrlBuilder(new HttpContextAccessor { HttpContext = httpContext }),
            options);
    }

    [Fact]
    public async Task Should_Upload_And_Return_The_File_With_Its_Url()
    {
        var file = await _tools.UploadFileAsync(Container, "cover.png", Base64("hello"));

        _created!.ContainerName.ShouldBe(Container);
        _created.File.ContentType.ShouldBe("image/png");
        Encoding.UTF8.GetString(_createdBytes!).ShouldBe("hello");
        file.Url.ShouldBe($"https://example.test/{FileExplorerRemoteServiceConsts.FilesRoutePrefix}/{Container}/2026/cover.png?__tenant=");
    }

    [Fact]
    public async Task Should_Accept_A_Data_Url_And_Use_Its_Declared_Type()
    {
        await _tools.UploadFileAsync(Container, "cover", "data:image/webp;base64," + Base64("hello"));

        _created!.File.ContentType.ShouldBe("image/webp");
        Encoding.UTF8.GetString(_createdBytes!).ShouldBe("hello");
    }

    [Fact]
    public async Task Should_Use_The_Container_Name_As_Configured()
    {
        await _tools.UploadFileAsync("SITE-IMAGES", "cover.png", Base64("hello"));

        _created!.ContainerName.ShouldBe(Container);
    }

    [Fact]
    public async Task Should_Refuse_A_Container_That_Is_Not_Exposed_And_Name_The_Ones_That_Are()
    {
        var exception = await Should.ThrowAsync<FileExplorerMcpNotFoundException>(() =>
            _tools.UploadFileAsync("private-documents", "a.txt", Base64("hello")));

        exception.Message.ShouldContain("'site-images'");
        await _fileAppService.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task Should_Refuse_An_Oversized_Upload_Before_Decoding_It()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(() =>
            _tools.UploadFileAsync(Container, "big.bin", Base64(new string('x', 64))));

        exception.ValidationErrors.Single().MemberNames.ShouldBe(new[] { "contentBase64" });
        await _fileAppService.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task Should_Apply_A_Container_Limit_Stricter_Than_The_Module_Limit()
    {
        _fileAppService.GetFileContainerConfigurationAsync(Container)
            .Returns(new FileContainerConfigurationDto { MaxBlobSize = 4 });

        await Should.ThrowAsync<AbpValidationException>(() =>
            _tools.UploadFileAsync(Container, "a.txt", Base64("hello")));
    }

    /// <summary>
    /// The size estimate made before decoding must not count the '=' padding, or the whitespace of
    /// line-wrapped base64: either would refuse a file that is exactly at the limit.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_Accept_A_File_Exactly_At_The_Limit(bool lineWrapped)
    {
        var content = Base64("sixteen bytes!!!");
        if (lineWrapped)
        {
            content = content.Insert(12, "\r\n");
        }

        await _tools.UploadFileAsync(Container, "a.txt", content);

        _createdBytes!.Length.ShouldBe(16);
    }

    [Fact]
    public async Task Should_Report_Invalid_Base64_As_A_Validation_Error()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(() =>
            _tools.UploadFileAsync(Container, "a.txt", "not base64!"));

        exception.ValidationErrors.Single().MemberNames.ShouldBe(new[] { "contentBase64" });
    }

    [Fact]
    public async Task Should_Treat_A_File_In_An_Unexposed_Container_As_Missing()
    {
        var id = Guid.NewGuid();
        _fileAppService.GetAsync(id).Returns(new FileDescriptorDto { ContainerName = "private-documents" });

        var exception = await Should.ThrowAsync<FileExplorerMcpNotFoundException>(() => _tools.GetFileAsync(id));

        exception.Message.ShouldBe($"There is no file with id '{id}'.");
    }

    /// <summary>
    /// Forbidden, missing and unexposed must be indistinguishable, or probing ids reveals which exist.
    /// </summary>
    [Fact]
    public async Task Should_Report_A_Forbidden_File_Exactly_Like_A_Missing_One()
    {
        var forbiddenId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        _fileAppService.GetAsync(forbiddenId).Returns<Task<FileDescriptorDto>>(_ => throw new AbpAuthorizationException("Authorization failed!"));
        _fileAppService.GetAsync(missingId).Returns<Task<FileDescriptorDto>>(_ => throw new EntityNotFoundException(typeof(object), missingId));

        var forbidden = await Should.ThrowAsync<FileExplorerMcpNotFoundException>(() => _tools.GetFileAsync(forbiddenId));
        var missing = await Should.ThrowAsync<FileExplorerMcpNotFoundException>(() => _tools.GetFileAsync(missingId));

        forbidden.Message.ShouldBe($"There is no file with id '{forbiddenId}'.");
        missing.Message.ShouldBe($"There is no file with id '{missingId}'.");
        forbidden.Code.ShouldBe(missing.Code);
    }

    [Fact]
    public async Task Should_Not_Delete_A_File_In_An_Unexposed_Container()
    {
        var id = Guid.NewGuid();
        _fileAppService.GetAsync(id).Returns(new FileDescriptorDto { ContainerName = "private-documents" });

        await Should.ThrowAsync<FileExplorerMcpNotFoundException>(() => _tools.DeleteFileAsync(id));

        await _fileAppService.DidNotReceive().DeleteAsync(id);
    }

    /// <summary>
    /// A rename must not clear the directory or the cell: UpdateFileInput only applies what was assigned,
    /// so nothing else may be assigned.
    /// </summary>
    [Fact]
    public async Task Should_Change_Only_What_Was_Passed()
    {
        var id = ArrangeExposedFile();

        await _tools.UpdateFileAsync(id, name: "renamed.png");

        await _fileAppService.Received().UpdateAsync(id, Arg.Is<UpdateFileInput>(input =>
            input.Name == "renamed.png" && !input.DirectoryIdSpecified && !input.CellNameSpecified));
    }

    [Fact]
    public async Task Should_Move_To_The_Root_Only_When_Asked()
    {
        var id = ArrangeExposedFile();

        await _tools.UpdateFileAsync(id, moveToRoot: true);

        await _fileAppService.Received().UpdateAsync(id, Arg.Is<UpdateFileInput>(input =>
            input.DirectoryIdSpecified && input.DirectoryId == null && input.Name == null));
    }

    [Fact]
    public async Task Should_Reject_Moving_To_A_Directory_And_The_Root_At_Once()
    {
        await Should.ThrowAsync<AbpValidationException>(() =>
            _tools.UpdateFileAsync(Guid.NewGuid(), directoryId: Guid.NewGuid(), moveToRoot: true));
    }

    private Guid ArrangeExposedFile()
    {
        var id = Guid.NewGuid();
        _fileAppService.GetAsync(id).Returns(new FileDescriptorDto { ContainerName = Container });
        _fileAppService.UpdateAsync(id, Arg.Any<UpdateFileInput>()).Returns(new FileDescriptorDto { ContainerName = Container });
        return id;
    }

    private static string Base64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
}
