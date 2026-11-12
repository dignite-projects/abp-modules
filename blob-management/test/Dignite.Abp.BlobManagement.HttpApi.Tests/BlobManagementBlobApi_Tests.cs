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

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The BLOB API end to end over HTTP: an application's container on the remote provider (the generated C# client
/// proxies), and the API's authorization of its callers.
/// </summary>
public class BlobManagementBlobApi_Tests : AbpWebApplicationFactoryIntegratedTest<Program>
{
    private const string ApiPath = "/api/blob-management/blobs/";

    private readonly IBlobContainerFactory _blobContainerFactory;
    private readonly ICurrentTenant _currentTenant;

    public BlobManagementBlobApi_Tests()
    {
        _blobContainerFactory = GetRequiredService<IBlobContainerFactory>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Should_Round_Trip_Through_The_Remote_BlobManagement_As_An_Allowed_Client()
    {
        var bytes = BlobManagementTestFiles.Pdf("remote " + Guid.NewGuid());
        var container = _blobContainerFactory.Create(BlobManagementHttpApiTestModule.AppContainer);

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            await container.SaveAsync("2026/11/report.pdf", bytes);

            (await container.ExistsAsync("2026/11/report.pdf")).ShouldBeTrue();
            (await container.GetAllBytesOrNullAsync("2026/11/report.pdf")).ShouldBe(bytes);

            var file = await FindInBlobManagementAsync(BlobManagementHttpApiTestModule.OpenContainer, "2026/11/report.pdf");
            file.ShouldNotBeNull();
            file!.ClientId.ShouldBe(BlobManagementHttpApiTestModule.AllowedClient);
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
        var container = _blobContainerFactory.Create(BlobManagementHttpApiTestModule.AppContainer);
        var name = Guid.NewGuid().ToString("N") + ".pdf";

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            await container.SaveAsync(name, BlobManagementTestFiles.Pdf("first " + name));

            await Should.ThrowAsync<BlobAlreadyExistsException>(() =>
                container.SaveAsync(name, BlobManagementTestFiles.Pdf("second " + name)));

            var second = BlobManagementTestFiles.Pdf("second " + name);
            await container.SaveAsync(name, second, overrideExisting: true);
            (await container.GetAllBytesAsync(name)).ShouldBe(second);
        }
    }

    [Fact]
    public async Task The_Tenant_Should_Travel_With_The_Call()
    {
        var bytes = BlobManagementTestFiles.Pdf("tenant " + Guid.NewGuid());

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        using (_currentTenant.Change(BlobManagementHttpApiTestModule.TenantId))
        {
            await _blobContainerFactory.Create(BlobManagementHttpApiTestModule.AppContainer).SaveAsync("tenant.pdf", bytes);
            (await FindInBlobManagementAsync(BlobManagementHttpApiTestModule.OpenContainer, "tenant.pdf"))!.TenantId
                .ShouldBe(BlobManagementHttpApiTestModule.TenantId);
        }

        (await FindInBlobManagementAsync(BlobManagementHttpApiTestModule.OpenContainer, "tenant.pdf")).ShouldBeNull();
    }

    [Fact]
    public async Task A_Client_The_Container_Does_Not_Allow_Should_Be_Refused()
    {
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            var exception = await Should.ThrowAsync<AbpRemoteCallException>(() =>
                _blobContainerFactory.Create(BlobManagementHttpApiTestModule.AppClosedContainer).SaveAsync("x.pdf", BlobManagementTestFiles.Pdf("x")));
            exception.HttpStatusCode.ShouldBe((int)HttpStatusCode.Forbidden);
        }

        (await GetExistsAsync(BlobManagementHttpApiTestModule.OpenContainer, clientId: "another-app")).ShouldBe(HttpStatusCode.Forbidden);
        (await GetExistsAsync(BlobManagementHttpApiTestModule.DocumentsContainer, clientId: BlobManagementHttpApiTestModule.AllowedClient)).ShouldBe(HttpStatusCode.Forbidden);
        (await GetExistsAsync("undeclared", clientId: BlobManagementHttpApiTestModule.AllowedClient)).ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_Token_That_Carries_A_User_Should_Be_Refused_Even_From_An_Allowed_Client()
    {
        (await GetExistsAsync(BlobManagementHttpApiTestModule.OpenContainer, BlobManagementHttpApiTestModule.AllowedClient, Guid.NewGuid()))
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_Anonymous_Caller_Should_Be_Refused()
    {
        (await GetExistsAsync(BlobManagementHttpApiTestModule.OpenContainer, clientId: null)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_Allowed_Client_Should_Be_Answered()
    {
        (await GetExistsAsync(BlobManagementHttpApiTestModule.OpenContainer, BlobManagementHttpApiTestModule.AllowedClient)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_Missing_File_Should_Be_404_With_The_Drives_Code()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiPath + BlobManagementHttpApiTestModule.OpenContainer + "?blobName=missing.pdf");
        request.Headers.Add(TestCaller.ClientHeader, BlobManagementHttpApiTestModule.AllowedClient);

        using var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain(BlobManagementErrorCodes.BlobNotFound);
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

    private async Task<FileDescriptor?> FindInBlobManagementAsync(string driveContainer, string name)
    {
        using var uow = GetRequiredService<IUnitOfWorkManager>().Begin();
        var file = await GetRequiredService<FileDescriptorManager>().FindAsync(driveContainer, name);
        await uow.CompleteAsync();
        return file;
    }
}
