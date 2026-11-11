using System;
using System.IO;
using System.Threading.Tasks;
using Dignite.Drive.Containers;
using Dignite.Drive.Files;
using Dignite.Drive.Tickets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.Validation;
using Xunit;

namespace Dignite.Drive.Mcp;

/// <summary>
/// What the file tools add to the file service: the upload link and its commands, and inline uploads within their
/// limits.
/// </summary>
public class DriveFileTools_Tests
{
    private const string Container = "documents";

    private readonly IDriveFileAppService _files = Substitute.For<IDriveFileAppService>();
    private readonly IDriveContainerAppService _containers = Substitute.For<IDriveContainerAppService>();
    private readonly DriveMcpOptions _options = new() { MaxInlineUploadSize = 16 };
    private CreateDriveFileInput? _created;
    private byte[]? _createdBytes;

    public DriveFileTools_Tests()
    {
        _containers.GetAsync(Container).Returns(new DriveContainerDto { Name = Container, MaxFileSize = 1024 });
        _files.CreateAsync(Arg.Do<CreateDriveFileInput>(input =>
            {
                _created = input;
                using var memory = new MemoryStream();
                input.File.GetStream().CopyTo(memory);
                _createdBytes = memory.ToArray();
            }))
            .Returns(callInfo => new DriveFileDto { Name = callInfo.Arg<CreateDriveFileInput>().Name! });
        _files.CreateUploadTicketAsync(Arg.Any<CreateDriveFileUploadTicketInput>())
            .Returns(new DriveTicketDto
            {
                Ticket = "abc",
                Kind = DriveTicketKind.FileUpload,
                ExpiresAt = new DateTime(2026, 11, 10, 12, 0, 0),
                Url = DriveTicketUrl.BuildFileUpload("abc")
            });
    }

    [Fact]
    public async Task An_Upload_Link_Should_Point_At_The_Configured_Drive_With_Commands_That_Post_The_File()
    {
        _options.BaseUrl = "https://drive.example.com/";

        var link = await Tools().CreateUploadUrlAsync(Container, " minutes.pdf ");

        link.Url.ShouldBe("https://drive.example.com" + DriveTicketUrl.FileUploadPath + "?ticket=abc");
        link.Method.ShouldBe("POST");
        link.FormField.ShouldBe("content");
        link.FileName.ShouldBe("minutes.pdf");
        link.Command.ShouldBe($"curl -sS --fail-with-body -F \"content=@FILE_PATH\" \"{link.Url}\"");
        link.PowerShellCommand.ShouldStartWith("curl.exe ");
        await _files.Received().CreateUploadTicketAsync(Arg.Is<CreateDriveFileUploadTicketInput>(input =>
            input.ContainerName == Container && input.FileName == " minutes.pdf "));
    }

    [Fact]
    public async Task An_Upload_Link_Should_Default_To_The_Address_The_Request_Came_In_On()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("files.example.com");
        context.Request.PathBase = "/app";

        var link = await Tools(new HttpContextAccessor { HttpContext = context }).CreateUploadUrlAsync(Container, "a.pdf");

        link.Url.ShouldStartWith("https://files.example.com/app" + DriveTicketUrl.FileUploadPath);
    }

    [Fact]
    public async Task An_Inline_Upload_Should_Be_Decoded_And_Named()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        (await Tools().UploadFileAsync(Container, "tiny.bin", "data:application/octet-stream;base64," + Convert.ToBase64String(bytes)))
            .Name.ShouldBe("tiny.bin");

        _created!.ContainerName.ShouldBe(Container);
        _created.Name.ShouldBe("tiny.bin");
        _createdBytes.ShouldBe(bytes);
    }

    [Fact]
    public async Task An_Oversized_Or_Broken_Inline_Upload_Should_Be_Refused_Before_It_Is_Stored()
    {
        await Should.ThrowAsync<AbpValidationException>(() =>
            Tools().UploadFileAsync(Container, "big.bin", Convert.ToBase64String(new byte[17])));
        await Should.ThrowAsync<AbpValidationException>(() => Tools().UploadFileAsync(Container, "bad.bin", "not base64!"));

        // The container's own cap, when it is the smaller.
        _containers.GetAsync(Container).Returns(new DriveContainerDto { Name = Container, MaxFileSize = 4 });
        await Should.ThrowAsync<AbpValidationException>(() =>
            Tools().UploadFileAsync(Container, "five.bin", Convert.ToBase64String(new byte[5])));

        await _files.DidNotReceive().CreateAsync(Arg.Any<CreateDriveFileInput>());
    }

    private DriveFileTools Tools(IHttpContextAccessor? httpContextAccessor = null)
    {
        return new DriveFileTools(_files, _containers, Options.Create(_options), httpContextAccessor);
    }
}
