using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Blobs;
using Dignite.Abp.BlobManagement.Containers;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Permissions;
using Dignite.Abp.BlobManagement.Usage;
using Shouldly;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Content;
using Volo.Abp.Guids;
using Volo.Abp.Http.Client;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The file API over HTTP, through the generated C# client proxies: an application keeps files as an OAuth client
/// (<see cref="IBlobManagementFileService"/>, the remote one), and an administrator reads them - and the usage - with
/// the management permission.
/// </summary>
public class BlobManagementFileApi_Tests : AbpWebApplicationFactoryIntegratedTest<Program>
{
    private const string UiClient = "blob-ui";
    private const string Container = BlobManagementHttpApiTestModule.OpenContainer;

    private readonly IBlobManagementFileService _appFiles;
    private readonly IBlobManagementBlobService _appBlobs;
    private readonly BlobManagementFileClientProxy _files;
    private readonly BlobManagementContainerClientProxy _containers;
    private readonly BlobManagementUsageClientProxy _usage;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _appUser = Guid.NewGuid();

    public BlobManagementFileApi_Tests()
    {
        _appFiles = GetRequiredService<IBlobManagementFileService>();
        _appBlobs = GetRequiredService<IBlobManagementBlobService>();
        _files = GetRequiredService<BlobManagementFileClientProxy>();
        _containers = GetRequiredService<BlobManagementContainerClientProxy>();
        _usage = GetRequiredService<BlobManagementUsageClientProxy>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task A_Granted_Client_Should_Keep_A_Tenants_Files_By_Its_Grant_On_The_Host_Side()
    {
        // The grant is the host's (the client provider writes it there); the call names the tenant in __tenant, which
        // ABP's client proxy sends for the current tenant, and the token names none.
        using (_currentTenant.Change(BlobManagementHttpApiTestModule.TenantId))
        {
            BlobManagementFileDto file;
            using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
            {
                file = await _files.CreateAsync(NewFile(Container, "tenant.pdf"));
                (await _files.GetAsync(file.Id)).Name.ShouldBe("tenant.pdf");
                (await _files.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container, Filter = "tenant.pdf" }))
                    .Items.Select(f => f.Id).ShouldContain(file.Id);
            }

            using (TestCaller.As("another-app"))
            {
                await ShouldFailAsync(HttpStatusCode.Forbidden, () => _files.CreateAsync(NewFile(Container, "a.pdf")));
                await ShouldFailAsync(HttpStatusCode.NotFound, () => _files.GetAsync(file.Id));
            }
        }
    }

    [Fact]
    public async Task An_Application_Should_Keep_Files_As_A_Client_And_Name_Its_User()
    {
        _appFiles.ShouldBeOfType<RemoteBlobManagementFileService>();
        var bytes = BlobManagementTestFiles.Pdf("over http " + Guid.NewGuid());

        // The application calls with its own credentials - a token without a user - while its user is signed in to it.
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        using (AsApplicationUser(_appUser))
        {
            var file = await _appFiles.CreateAsync(NewFile(Container, "report.pdf", bytes));
            file.Name.ShouldBe("report.pdf");
            file.ClientId.ShouldBe(BlobManagementHttpApiTestModule.AllowedClient);
            // The token carries no user: the client named the application's current user.
            file.CreatorId.ShouldBe(_appUser);

            (await _appFiles.GetAsync(file.Id)).Name.ShouldBe("report.pdf");
            using (var content = await _appFiles.GetContentAsync(file.Id))
            {
                using var memory = new MemoryStream();
                await content.GetStream().CopyToAsync(memory);
                memory.ToArray().ShouldBe(bytes);
            }

            (await _appFiles.UpdateAsync(file.Id, new UpdateBlobManagementFileInput { Name = "final.pdf" })).Name.ShouldBe("final.pdf");
            (await _appFiles.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container, Filter = "FINAL" }))
                .Items.Select(f => f.Id).ShouldContain(file.Id);

            await _appFiles.DeleteAsync(file.Id);
            await ShouldFailAsync(HttpStatusCode.NotFound, () => _appFiles.GetAsync(file.Id));
            // Deleting what is gone already is not an error.
            await _appFiles.DeleteAsync(file.Id);

            // A BLOB saved under a name is counted for the application's user as well.
            var blobName = "avatars/" + Guid.NewGuid().ToString("N") + ".pdf";
            await _appBlobs.SaveAsync(Container, blobName, new RemoteStreamContent(new MemoryStream(bytes), blobName, "application/pdf"));
            var saved = (await _appFiles.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container, Filter = blobName })).Items.Single();
            saved.IsAddressedByName.ShouldBeTrue();
            saved.CreatorId.ShouldBe(_appUser);
            saved.ClientId.ShouldBe(BlobManagementHttpApiTestModule.AllowedClient);
        }
    }

    [Fact]
    public async Task The_File_Api_Should_Refuse_Whoever_Is_Not_An_Allowed_Client_To_Change_A_File()
    {
        await GrantAsync(_admin, BlobManagementPermissions.GetAll());

        BlobManagementFileDto file;
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            file = await _files.CreateAsync(NewFile(Container, "theirs.pdf"));

            // A container that lists no client takes nothing from one - and one the host never declared answers the
            // same, as on the BLOB API.
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _files.CreateAsync(NewFile(BlobManagementHttpApiTestModule.ClosedContainer, "a.pdf")));
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _files.CreateAsync(NewFile("undeclared", "a.pdf")));
        }

        // Another client: the file is not found, and deleting it does nothing.
        using (TestCaller.As("another-app"))
        {
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _files.CreateAsync(NewFile(Container, "a.pdf")));
            await ShouldFailAsync(HttpStatusCode.NotFound, () => _files.GetAsync(file.Id));
            await _files.DeleteAsync(file.Id);
        }

        // A token that carries a user is no client's, whatever the user holds and whichever client issued it.
        foreach (var client in new[] { UiClient, BlobManagementHttpApiTestModule.AllowedClient })
        {
            using (TestCaller.As(client, _admin))
            {
                await ShouldFailAsync(HttpStatusCode.Forbidden, () => _files.CreateAsync(NewFile(Container, "a.pdf")));
                await ShouldFailAsync(HttpStatusCode.Forbidden,
                    () => _files.UpdateAsync(file.Id, new UpdateBlobManagementFileInput { Name = "mine.pdf" }));
                await ShouldFailAsync(HttpStatusCode.Forbidden,
                    () => _files.MoveAsync(file.Id, new MoveBlobManagementFileInput { DirectoryId = null }));
                await ShouldFailAsync(HttpStatusCode.Forbidden, () => _files.DeleteAsync(file.Id));
            }
        }

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            (await _files.GetAsync(file.Id)).Name.ShouldBe("theirs.pdf");
        }
    }

    [Fact]
    public async Task An_Administrator_Should_Read_Files_And_Their_Bytes_As_An_Attachment()
    {
        await GrantAsync(_admin, BlobManagementPermissions.Containers.Default);

        BlobManagementFileDto file;
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            file = await _files.CreateAsync(NewFile(BlobManagementHttpApiTestModule.DocumentsContainer, "page.pdf"));
        }

        using (TestCaller.As(UiClient, _admin))
        {
            // The management permission sees every container the host declares, and no other.
            var containers = (await _containers.GetListAsync()).Items.Select(c => c.Name).ToList();
            containers.ShouldContain(BlobManagementHttpApiTestModule.DocumentsContainer);
            containers.ShouldContain(BlobManagementHttpApiTestModule.ClosedContainer);
            containers.ShouldNotContain("undeclared");

            (await _files.GetAsync(file.Id)).Name.ShouldBe("page.pdf");
            (await _files.GetListAsync(new GetBlobManagementFilesInput { ContainerName = BlobManagementHttpApiTestModule.DocumentsContainer }))
                .Items.Select(f => f.Id).ShouldContain(file.Id);
            await ShouldFailAsync(HttpStatusCode.BadRequest,
                () => _files.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container, Sorting = "Hash" }),
                BlobManagementErrorCodes.SortingNotSupported);
        }

        using (var request = new HttpRequestMessage(HttpMethod.Get, $"/api/blob-management/files/{file.Id}/content"))
        {
            request.Headers.Add(TestCaller.UserHeader, _admin.ToString());
            using var response = await Client.SendAsync(request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
            response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        }

        // A signed-in user without the permission reads nothing by an id, and an anonymous caller is not let in.
        using (var request = new HttpRequestMessage(HttpMethod.Get, $"/api/blob-management/files/{file.Id}/content"))
        {
            request.Headers.Add(TestCaller.UserHeader, Guid.NewGuid().ToString());
            using var response = await Client.SendAsync(request);
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var request = new HttpRequestMessage(HttpMethod.Get, $"/api/blob-management/files/{file.Id}/content"))
        {
            using var response = await Client.SendAsync(request);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (TestCaller.As(UiClient, Guid.NewGuid()))
        {
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _files.GetAsync(file.Id));
            await ShouldFailAsync(HttpStatusCode.Forbidden,
                () => _files.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container }));
        }
    }

    [Fact]
    public async Task An_Administrator_Should_Read_The_Usage()
    {
        await GrantAsync(_admin, BlobManagementPermissions.Containers.Default);
        var bytes = BlobManagementTestFiles.Pdf("counted " + Guid.NewGuid());

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        using (AsApplicationUser(_appUser))
        {
            await _appFiles.CreateAsync(NewFile(Container, "counted.pdf", bytes));
        }

        using (TestCaller.As(UiClient, _admin))
        {
            var usage = await _usage.GetAsync();
            usage.StoredBytes.ShouldBeGreaterThanOrEqualTo(bytes.Length);
            usage.StorageSize.ShouldBeNull();
            usage.Containers.Single(c => c.ContainerName == Container).StoredBytes.ShouldBeGreaterThanOrEqualTo(bytes.Length);
            usage.Containers.Select(c => c.ContainerName).ShouldContain(BlobManagementHttpApiTestModule.ClosedContainer);
            usage.Containers.Sum(c => c.StoredBytes).ShouldBe(usage.StoredBytes);

            var users = await _usage.GetUsersAsync(new GetBlobManagementUserUsagesInput { ContainerName = Container });
            users.Items.Single(u => u.UserId == _appUser).UploadedBytes.ShouldBe(bytes.Length);

            var clients = await _usage.GetClientsAsync(new GetBlobManagementClientUsagesInput { ContainerName = Container });
            clients.Items.Single(c => c.ClientId == BlobManagementHttpApiTestModule.AllowedClient)
                .UploadedBytes.ShouldBeGreaterThanOrEqualTo(bytes.Length);
        }

        // Not without the permission, and not for a client.
        using (TestCaller.As(UiClient, Guid.NewGuid()))
        {
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _usage.GetAsync());
        }

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _usage.GetAsync());
        }
    }

    private static CreateBlobManagementFileInput NewFile(string containerName, string name, byte[]? bytes = null)
    {
        return new CreateBlobManagementFileInput
        {
            ContainerName = containerName,
            File = new RemoteStreamContent(
                new MemoryStream(bytes ?? BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString())), name, "application/pdf")
        };
    }

    /// <summary>The user signed in to the application - its side of the call, not what Blob Management is sent.</summary>
    private IDisposable AsApplicationUser(Guid userId)
    {
        return _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) }, "Application")));
    }

    private async Task GrantAsync(Guid userId, params string[] permissions)
    {
        using var uow = GetRequiredService<IUnitOfWorkManager>().Begin();
        foreach (var permission in permissions)
        {
            await GetRequiredService<IPermissionGrantRepository>().InsertAsync(new PermissionGrant(
                GetRequiredService<IGuidGenerator>().Create(), permission, "U", userId.ToString()));
        }

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
