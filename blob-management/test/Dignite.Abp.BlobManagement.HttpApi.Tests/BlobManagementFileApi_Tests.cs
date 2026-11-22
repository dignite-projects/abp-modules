using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
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
/// through the integration service (<c>integration-api/blob-management/files</c>; <see cref="IBlobManagementFileService"/>,
/// the remote one, or its proxy), and an administrator reads them - and the usage - with the management permission,
/// through the service for users (<c>api/blob-management/files</c>, <see cref="IBlobManagementFileAppService"/>).
/// </summary>
public class BlobManagementFileApi_Tests : AbpWebApplicationFactoryIntegratedTest<Program>
{
    private const string UiClient = "blob-ui";
    private const string Container = BlobManagementHttpApiTestModule.OpenContainer;

    private readonly IBlobManagementFileService _appFiles;
    private readonly IBlobManagementBlobService _appBlobs;
    private readonly BlobManagementFileIntegrationClientProxy _integration;
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
        _integration = GetRequiredService<BlobManagementFileIntegrationClientProxy>();
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
                file = await _integration.CreateAsync(NewFile(Container, "tenant.pdf"));
                (await _integration.GetAsync(file.Id)).Name.ShouldBe("tenant.pdf");
                (await _integration.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container, Filter = "tenant.pdf" }))
                    .Items.Select(f => f.Id).ShouldContain(file.Id);
            }

            using (TestCaller.As("another-app"))
            {
                await ShouldFailAsync(HttpStatusCode.Forbidden, () => _integration.CreateAsync(NewFile(Container, "a.pdf")));
                await ShouldFailAsync(HttpStatusCode.NotFound, () => _integration.GetAsync(file.Id));
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
            file = await _integration.CreateAsync(NewFile(Container, "theirs.pdf"));

            // A container that lists no client takes nothing from one - and one the host never declared answers the
            // same, as on the BLOB API.
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _integration.CreateAsync(NewFile(BlobManagementHttpApiTestModule.ClosedContainer, "a.pdf")));
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _integration.CreateAsync(NewFile("undeclared", "a.pdf")));
        }

        // Another client: the file is not found, and deleting it does nothing.
        using (TestCaller.As("another-app"))
        {
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _integration.CreateAsync(NewFile(Container, "a.pdf")));
            await ShouldFailAsync(HttpStatusCode.NotFound, () => _integration.GetAsync(file.Id));
            await _integration.DeleteAsync(file.Id);
        }

        // A token that carries a user is no client's, whatever the user holds - here every permission, the management
        // one among them - and whichever client issued it: the integration service lets it neither write nor read.
        foreach (var client in new[] { UiClient, BlobManagementHttpApiTestModule.AllowedClient })
        {
            using (TestCaller.As(client, _admin))
            {
                await ShouldFailAsync(HttpStatusCode.Forbidden, () => _integration.CreateAsync(NewFile(Container, "a.pdf")));
                await ShouldFailAsync(HttpStatusCode.Forbidden,
                    () => _integration.UpdateAsync(file.Id, new UpdateBlobManagementFileInput { Name = "mine.pdf" }));
                await ShouldFailAsync(HttpStatusCode.Forbidden,
                    () => _integration.MoveAsync(file.Id, new MoveBlobManagementFileInput { DirectoryId = null }));
                await ShouldFailAsync(HttpStatusCode.Forbidden, () => _integration.DeleteAsync(file.Id));

                await ShouldFailAsync(HttpStatusCode.Forbidden, () => _integration.GetAsync(file.Id));
                await ShouldFailAsync(HttpStatusCode.Forbidden,
                    () => _integration.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container }));
                await ShouldFailAsync(HttpStatusCode.Forbidden, () => _integration.GetContentAsync(file.Id));
            }
        }

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            (await _integration.GetAsync(file.Id)).Name.ShouldBe("theirs.pdf");
        }
    }

    [Fact]
    public async Task An_Administrator_Should_Read_Files_And_Their_Bytes_As_An_Attachment()
    {
        await GrantAsync(_admin, BlobManagementPermissions.Containers.Default);
        var bytes = BlobManagementTestFiles.Pdf("page " + Guid.NewGuid());

        BlobManagementFileDto file;
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            file = await _integration.CreateAsync(NewFile(BlobManagementHttpApiTestModule.DocumentsContainer, "page.pdf", bytes));
        }

        // The management page's own service, api/blob-management/files: what a client stored, listed, shown and read.
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
            using (var content = await _files.GetContentAsync(file.Id))
            {
                using var memory = new MemoryStream();
                await content.GetStream().CopyToAsync(memory);
                memory.ToArray().ShouldBe(bytes);
            }

            await ShouldFailAsync(HttpStatusCode.BadRequest,
                () => _files.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container, Sorting = "Hash" }),
                BlobManagementErrorCodes.SortingNotSupported);
        }

        // A client acting for itself is no user: the management page's service lets it read nothing, whatever it may
        // do through the integration service.
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            await ShouldFailAsync(HttpStatusCode.Forbidden,
                () => _files.GetListAsync(new GetBlobManagementFilesInput { ContainerName = BlobManagementHttpApiTestModule.DocumentsContainer }));
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _files.GetAsync(file.Id));
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
            users.Items.Single(u => u.UserId == _appUser).StoredBytes.ShouldBe(bytes.Length);

            var clients = await _usage.GetClientsAsync(new GetBlobManagementClientUsagesInput { ContainerName = Container });
            clients.Items.Single(c => c.ClientId == BlobManagementHttpApiTestModule.AllowedClient)
                .StoredBytes.ShouldBeGreaterThanOrEqualTo(bytes.Length);
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

    [Fact]
    public async Task The_Host_Should_Read_The_Usage_By_Tenant_Over_HTTP()
    {
        var reader = Guid.NewGuid();
        await GrantAsync(reader, BlobManagementPermissions.Containers.Default);
        var input = new GetBlobManagementTenantUsagesInput { MaxResultCount = 1000 };

        // The management permission is not enough: the breakdown crosses tenants.
        using (TestCaller.As(UiClient, reader))
        {
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _usage.GetTenantsAsync(input));
        }

        await GrantAsync(reader, BlobManagementPermissions.Containers.TenantUsages);
        using (TestCaller.As(UiClient, reader))
        {
            var tenants = await _usage.GetTenantsAsync(input);
            tenants.TotalCount.ShouldBe(tenants.Items.Count);
            tenants.Items.Select(t => t.StoredBytes).ShouldBe(tenants.Items.Select(t => t.StoredBytes).OrderByDescending(bytes => bytes));
        }

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            await ShouldFailAsync(HttpStatusCode.Forbidden, () => _usage.GetTenantsAsync(input));
        }
    }

    /// <summary>
    /// The SHA-256 of the file travels as the form field <c>sha256</c> of the file API: a different one is a 400 and
    /// leaves no file, the right one stores it.
    /// </summary>
    [Fact]
    public async Task A_Checksum_Sent_With_A_File_Should_Be_Verified_Over_Http()
    {
        var bytes = BlobManagementTestFiles.Pdf("file checksum " + Guid.NewGuid());
        var checksum = Convert.ToBase64String(SHA256.HashData(bytes));

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        using (AsApplicationUser(_appUser))
        {
            var wrongInput = NewFile(Container, "checked.pdf", bytes);
            wrongInput.Sha256 = Convert.ToBase64String(SHA256.HashData(BlobManagementTestFiles.Pdf("another file")));
            var wrong = await Should.ThrowAsync<AbpRemoteCallException>(() => _appFiles.CreateAsync(wrongInput));
            wrong.HttpStatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            wrong.Error.Code.ShouldBe(BlobManagementErrorCodes.ChecksumMismatch);

            var rightInput = NewFile(Container, "checked.pdf", bytes);
            rightInput.Sha256 = checksum;
            var file = await _appFiles.CreateAsync(rightInput);
            file.Name.ShouldBe("checked.pdf");
        }
    }

    /// <summary>
    /// The remote services send the application's user without touching the caller's input: what goes over HTTP is a
    /// copy, so an input the caller keeps or sends again still names nobody.
    /// </summary>
    [Fact]
    public async Task The_Remote_Services_Should_Not_Change_The_Callers_Input()
    {
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        using (AsApplicationUser(_appUser))
        {
            var input = NewFile(Container, "untouched.pdf");
            var file = await _appFiles.CreateAsync(input);
            file.CreatorId.ShouldBe(_appUser);
            input.CreatorId.ShouldBeNull();

            var ticketInput = new CreateBlobManagementUploadTicketInput { ContainerName = Container };
            await _appFiles.CreateUploadTicketAsync(ticketInput);
            ticketInput.UploaderId.ShouldBeNull();
        }
    }

    /// <summary>
    /// A file stored without a named user is counted for the application's current user when that user belongs to the
    /// current tenant, as ABP's auditing records the creator in process; a user of another side - a host user working in
    /// a tenant - is not sent, and the file is counted for nobody. Both ways in follow it.
    /// </summary>
    [Fact]
    public async Task A_File_Should_Be_Counted_For_The_Current_User_Only_When_They_Belong_To_The_Current_Tenant()
    {
        var tenantId = BlobManagementHttpApiTestModule.TenantId;
        var tenantUser = Guid.NewGuid();
        var hostUser = Guid.NewGuid();
        var bytes = BlobManagementTestFiles.Pdf("tenant rule " + Guid.NewGuid());

        using (_currentTenant.Change(tenantId))
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            using (AsApplicationUser(tenantUser, tenantId))
            {
                (await _appFiles.CreateAsync(NewFile(Container, "tenant-user.pdf", bytes))).CreatorId.ShouldBe(tenantUser);
                (await SaveUnderNameAsync(bytes)).CreatorId.ShouldBe(tenantUser);
            }

            using (AsApplicationUser(hostUser))
            {
                (await _appFiles.CreateAsync(NewFile(Container, "host-user.pdf", bytes))).CreatorId.ShouldBeNull();
                (await SaveUnderNameAsync(bytes)).CreatorId.ShouldBeNull();

                // A user the caller names is sent as named, whatever the tenant.
                var named = NewFile(Container, "named.pdf", bytes);
                named.CreatorId = hostUser;
                (await _appFiles.CreateAsync(named)).CreatorId.ShouldBe(hostUser);
            }
        }
    }

    /// <summary>Saves a BLOB under a new name through the remote BLOB service and reads back its file.</summary>
    private async Task<BlobManagementFileDto> SaveUnderNameAsync(byte[] bytes)
    {
        var blobName = "counted/" + Guid.NewGuid().ToString("N") + ".pdf";
        await _appBlobs.SaveAsync(Container, blobName, new RemoteStreamContent(new MemoryStream(bytes), blobName, "application/pdf"));
        return (await _appFiles.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container, Filter = blobName })).Items.Single();
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
    private IDisposable AsApplicationUser(Guid userId, Guid? tenantId = null)
    {
        var claims = new List<Claim> { new(AbpClaimTypes.UserId, userId.ToString()) };
        if (tenantId.HasValue)
        {
            claims.Add(new Claim(AbpClaimTypes.TenantId, tenantId.Value.ToString()));
        }

        return _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "Application")));
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
