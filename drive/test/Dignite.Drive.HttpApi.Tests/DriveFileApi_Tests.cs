using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Dignite.Drive.Containers;
using Dignite.Drive.Files;
using Dignite.Drive.Permissions;
using Dignite.Drive.Tickets;
using Shouldly;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Content;
using Volo.Abp.Guids;
using Volo.Abp.Http.Client;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.Drive;

/// <summary>
/// The file API over HTTP: a signed-in user calls <c>api/drive/containers</c> and <c>/files</c> (upload, read, delete)
/// through the generated C# client proxies; an anonymous browser redeems a user's upload ticket.
/// </summary>
public class DriveFileApi_Tests : AbpWebApplicationFactoryIntegratedTest<Program>
{
    private const string UiClient = "drive-ui";

    private readonly DriveContainerClientProxy _containers;
    private readonly DriveFileClientProxy _files;

    private readonly Guid _user = Guid.NewGuid();

    public DriveFileApi_Tests()
    {
        _containers = GetRequiredService<DriveContainerClientProxy>();
        _files = GetRequiredService<DriveFileClientProxy>();
    }

    [Fact]
    public async Task A_User_Should_Manage_Files_Through_The_Proxies()
    {
        await GrantAsync(DrivePermissions.Files.Upload, _user);
        var bytes = DriveTestFiles.Pdf("over http " + Guid.NewGuid());

        using (TestCaller.As(UiClient, _user))
        {
            var containers = (await _containers.GetListAsync()).Items.Select(c => c.Name).ToList();
            containers.ShouldContain(DriveHttpApiTestModule.UserDriveContainer);
            containers.ShouldNotContain(DriveHttpApiTestModule.OpenContainer);

            var file = await _files.CreateAsync(new CreateDriveFileInput
            {
                ContainerName = DriveHttpApiTestModule.UserDriveContainer,
                File = new RemoteStreamContent(new MemoryStream(bytes), "report.pdf", "application/pdf")
            });
            file.Name.ShouldBe("report.pdf");
            (await _files.GetAsync(file.Id)).Name.ShouldBe("report.pdf");

            using (var content = await _files.GetContentAsync(file.Id))
            {
                using var memory = new MemoryStream();
                await content.GetStream().CopyToAsync(memory);
                memory.ToArray().ShouldBe(bytes);
            }

            await _files.DeleteAsync(file.Id);
            await ShouldFailAsync(HttpStatusCode.NotFound, () => _files.GetAsync(file.Id));
        }
    }

    [Fact]
    public async Task Deleting_Someone_Elses_File_Should_Need_The_Delete_Permission()
    {
        await GrantAsync(DrivePermissions.Files.Upload, _user);

        DriveFileDto file;
        using (TestCaller.As(UiClient, _user))
        {
            file = await _files.CreateAsync(new CreateDriveFileInput
            {
                ContainerName = DriveHttpApiTestModule.UserDriveContainer,
                File = new RemoteStreamContent(new MemoryStream(DriveTestFiles.Pdf(Guid.NewGuid().ToString())), "theirs.pdf", "application/pdf")
            });
        }

        var other = Guid.NewGuid();
        using (TestCaller.As(UiClient, other))
        {
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _files.DeleteAsync(file.Id));
        }

        await GrantAsync(DrivePermissions.Files.Delete, other);
        using (TestCaller.As(UiClient, other))
        {
            await _files.DeleteAsync(file.Id);
            await ShouldFailAsync(HttpStatusCode.NotFound, () => _files.GetAsync(file.Id));
        }
    }

    [Fact]
    public async Task A_Files_Bytes_Should_Be_An_Attachment_For_Signed_In_Users_Only()
    {
        await GrantAsync(DrivePermissions.Files.Upload, _user);

        DriveFileDto file;
        using (TestCaller.As(UiClient, _user))
        {
            file = await _files.CreateAsync(new CreateDriveFileInput
            {
                ContainerName = DriveHttpApiTestModule.UserDriveContainer,
                File = new RemoteStreamContent(new MemoryStream(DriveTestFiles.Pdf(Guid.NewGuid().ToString())), "page.pdf", "application/pdf")
            });
        }

        using (var request = new HttpRequestMessage(HttpMethod.Get, $"/api/drive/files/{file.Id}/content"))
        {
            request.Headers.Add(TestCaller.UserHeader, _user.ToString());
            using var response = await Client.SendAsync(request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
            response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        }

        // Any signed-in user reads it by its id; an anonymous caller does not.
        using (var request = new HttpRequestMessage(HttpMethod.Get, $"/api/drive/files/{file.Id}/content"))
        {
            request.Headers.Add(TestCaller.UserHeader, Guid.NewGuid().ToString());
            using var response = await Client.SendAsync(request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var request = new HttpRequestMessage(HttpMethod.Get, $"/api/drive/files/{file.Id}/content"))
        {
            using var response = await Client.SendAsync(request);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task The_File_APIs_Errors_Should_Have_Their_Statuses()
    {
        await GrantAsync(DrivePermissions.Files.Upload, _user);

        // A client acting for itself is not a user.
        using (TestCaller.As(DriveHttpApiTestModule.AllowedClient))
        {
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _files.CreateUploadTicketAsync(new CreateDriveFileUploadTicketInput
            {
                ContainerName = DriveHttpApiTestModule.UserDriveContainer,
                FileName = "a.pdf"
            }));
        }

        using (TestCaller.As(UiClient, _user))
        {
            await ShouldFailAsync(HttpStatusCode.NotFound, () => _files.CreateUploadTicketAsync(new CreateDriveFileUploadTicketInput
            {
                ContainerName = DriveHttpApiTestModule.OpenContainer,
                FileName = "a.pdf"
            }), DriveErrorCodes.ContainerNotFound);
        }
    }

    [Fact]
    public async Task A_Browser_Should_Add_A_File_With_A_Users_Ticket_Once()
    {
        await GrantAsync(DrivePermissions.Files.Upload, _user);

        DriveTicketDto ticket;
        using (TestCaller.As(UiClient, _user))
        {
            ticket = await _files.CreateUploadTicketAsync(new CreateDriveFileUploadTicketInput
            {
                ContainerName = DriveHttpApiTestModule.UserDriveContainer,
                FileName = "from-a-shell.pdf"
            });
        }

        ticket.Url.ShouldStartWith(DriveTicketUrl.FileUploadPath + "?ticket=");

        using (var response = await PostFileAsync(ticket.Url, DriveTestFiles.Pdf(Guid.NewGuid().ToString())))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).ShouldContain("from-a-shell.pdf");
        }

        using (var again = await PostFileAsync(ticket.Url, DriveTestFiles.Pdf(Guid.NewGuid().ToString())))
        {
            again.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await again.Content.ReadAsStringAsync()).ShouldContain(DriveErrorCodes.InvalidTicket);
        }

        // A user's ticket is not a system upload ticket.
        using var asSystemUpload = await PostFileAsync(
            DriveTicketUrl.Build(upload: true, ticket.Ticket), DriveTestFiles.Pdf(Guid.NewGuid().ToString()));
        asSystemUpload.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private Task<HttpResponseMessage> PostFileAsync(string url, byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var form = new MultipartFormDataContent { { file, "content", "anything.bin" } };
        return Client.PostAsync(url, form);
    }

    private async Task GrantAsync(string permission, Guid userId)
    {
        using var uow = GetRequiredService<IUnitOfWorkManager>().Begin();
        await GetRequiredService<IPermissionGrantRepository>().InsertAsync(new PermissionGrant(
            GetRequiredService<IGuidGenerator>().Create(), permission, "U", userId.ToString()));
        await uow.CompleteAsync();
    }

    private static async Task ShouldFailAsync(HttpStatusCode status, Func<Task> action, string? code = null)
    {
        var exception = await Should.ThrowAsync<AbpRemoteCallException>(action);
        exception.HttpStatusCode.ShouldBe((int)status);
        if (code != null)
        {
            exception.Error.ShouldNotBeNull().Code.ShouldBe(code);
        }
    }
}
