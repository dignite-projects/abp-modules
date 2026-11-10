using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.BlobStoring;
using Volo.Abp.Http.Client;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.Drive;

/// <summary>
/// The BLOB API end to end over HTTP: an application's container on the remote provider (the generated C# client
/// proxies), and the API's authorization of its callers.
/// </summary>
public class DriveBlobApi_Tests : AbpWebApplicationFactoryIntegratedTest<Program>
{
    private const string ApiPath = "/api/drive/blobs/";

    private readonly IBlobContainerFactory _blobContainerFactory;
    private readonly ICurrentTenant _currentTenant;

    public DriveBlobApi_Tests()
    {
        _blobContainerFactory = GetRequiredService<IBlobContainerFactory>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Should_Round_Trip_Through_The_Remote_Drive_As_An_Allowed_Client()
    {
        var bytes = DriveTestFiles.Pdf("remote " + Guid.NewGuid());
        var container = _blobContainerFactory.Create(DriveHttpApiTestModule.AppContainer);

        using (TestCaller.As(DriveHttpApiTestModule.AllowedClient))
        {
            await container.SaveAsync("2026/11/report.pdf", bytes);

            (await container.ExistsAsync("2026/11/report.pdf")).ShouldBeTrue();
            (await container.GetAllBytesOrNullAsync("2026/11/report.pdf")).ShouldBe(bytes);

            var file = await FindInDriveAsync(DriveHttpApiTestModule.OpenContainer, "2026/11/report.pdf");
            file.ShouldNotBeNull();
            file!.ClientId.ShouldBe(DriveHttpApiTestModule.AllowedClient);
            file.CreatorId.ShouldBeNull();

            (await container.DeleteAsync("2026/11/report.pdf")).ShouldBeTrue();
            (await container.ExistsAsync("2026/11/report.pdf")).ShouldBeFalse();
            (await container.GetAllBytesOrNullAsync("2026/11/report.pdf")).ShouldBeNull();
            (await container.DeleteAsync("2026/11/report.pdf")).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task An_Existing_Name_Should_Fail_As_Abp_Does_Over_Http_Too()
    {
        var container = _blobContainerFactory.Create(DriveHttpApiTestModule.AppContainer);
        var name = Guid.NewGuid().ToString("N") + ".pdf";

        using (TestCaller.As(DriveHttpApiTestModule.AllowedClient))
        {
            await container.SaveAsync(name, DriveTestFiles.Pdf("first " + name));

            await Should.ThrowAsync<BlobAlreadyExistsException>(() =>
                container.SaveAsync(name, DriveTestFiles.Pdf("second " + name)));

            var second = DriveTestFiles.Pdf("second " + name);
            await container.SaveAsync(name, second, overrideExisting: true);
            (await container.GetAllBytesAsync(name)).ShouldBe(second);
        }
    }

    [Fact]
    public async Task The_Tenant_Should_Travel_With_The_Call()
    {
        var bytes = DriveTestFiles.Pdf("tenant " + Guid.NewGuid());

        using (TestCaller.As(DriveHttpApiTestModule.AllowedClient))
        using (_currentTenant.Change(DriveHttpApiTestModule.TenantId))
        {
            await _blobContainerFactory.Create(DriveHttpApiTestModule.AppContainer).SaveAsync("tenant.pdf", bytes);
            (await FindInDriveAsync(DriveHttpApiTestModule.OpenContainer, "tenant.pdf"))!.TenantId
                .ShouldBe(DriveHttpApiTestModule.TenantId);
        }

        (await FindInDriveAsync(DriveHttpApiTestModule.OpenContainer, "tenant.pdf")).ShouldBeNull();
    }

    [Fact]
    public async Task A_Client_The_Container_Does_Not_Allow_Should_Be_Refused()
    {
        using (TestCaller.As(DriveHttpApiTestModule.AllowedClient))
        {
            var exception = await Should.ThrowAsync<AbpRemoteCallException>(() =>
                _blobContainerFactory.Create(DriveHttpApiTestModule.AppClosedContainer).SaveAsync("x.pdf", DriveTestFiles.Pdf("x")));
            exception.HttpStatusCode.ShouldBe((int)HttpStatusCode.Forbidden);
        }

        (await GetExistsAsync(DriveHttpApiTestModule.OpenContainer, clientId: "another-app")).ShouldBe(HttpStatusCode.Forbidden);
        (await GetExistsAsync(DriveHttpApiTestModule.UserDriveContainer, clientId: DriveHttpApiTestModule.AllowedClient)).ShouldBe(HttpStatusCode.Forbidden);
        (await GetExistsAsync("undeclared", clientId: DriveHttpApiTestModule.AllowedClient)).ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_Token_That_Carries_A_User_Should_Be_Refused_Even_From_An_Allowed_Client()
    {
        (await GetExistsAsync(DriveHttpApiTestModule.OpenContainer, DriveHttpApiTestModule.AllowedClient, Guid.NewGuid()))
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_Anonymous_Caller_Should_Be_Refused()
    {
        (await GetExistsAsync(DriveHttpApiTestModule.OpenContainer, clientId: null)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_Allowed_Client_Should_Be_Answered()
    {
        (await GetExistsAsync(DriveHttpApiTestModule.OpenContainer, DriveHttpApiTestModule.AllowedClient)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_Missing_File_Should_Be_404_With_The_Drives_Code()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiPath + DriveHttpApiTestModule.OpenContainer + "?blobName=missing.pdf");
        request.Headers.Add(TestCaller.ClientHeader, DriveHttpApiTestModule.AllowedClient);

        using var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain(DriveErrorCodes.BlobNotFound);
    }

    private async Task<HttpStatusCode> GetExistsAsync(string containerName, string? clientId, Guid? userId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiPath + containerName + "/exists?blobName=any.pdf");
        if (clientId != null)
        {
            request.Headers.Add(TestCaller.ClientHeader, clientId);
        }

        if (userId != null)
        {
            request.Headers.Add(TestCaller.UserHeader, userId.ToString());
        }

        using var response = await Client.SendAsync(request);
        return response.StatusCode;
    }

    private async Task<FileDescriptor?> FindInDriveAsync(string driveContainer, string name)
    {
        using var uow = GetRequiredService<IUnitOfWorkManager>().Begin();
        var file = await GetRequiredService<FileDescriptorManager>().FindAsync(driveContainer, name);
        await uow.CompleteAsync();
        return file;
    }
}
