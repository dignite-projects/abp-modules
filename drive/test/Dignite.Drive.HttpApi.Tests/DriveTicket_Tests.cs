using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Drive;
using Dignite.Drive.Tickets;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.BlobStoring;
using Volo.Abp.Http.Client;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.Drive;

/// <summary>
/// Tickets over HTTP: an application (an allowed client) asks for one through <see cref="IDriveTicketClient"/> and the
/// remote proxy; an anonymous browser redeems it.
/// </summary>
public class DriveTicket_Tests : AbpWebApplicationFactoryIntegratedTest<Program>
{
    private readonly IDriveTicketClient _ticketClient;
    private readonly IBlobContainerFactory _blobContainerFactory;
    private readonly ICurrentTenant _currentTenant;

    public DriveTicket_Tests()
    {
        _ticketClient = GetRequiredService<IDriveTicketClient>();
        _blobContainerFactory = GetRequiredService<IBlobContainerFactory>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task A_Browser_Should_Upload_With_A_Ticket_Once_In_The_Tickets_Tenant()
    {
        var bytes = DriveTestFiles.Pdf("ticket " + Guid.NewGuid());
        DriveTicketDto ticket;
        using (_currentTenant.Change(DriveHttpApiTestModule.TenantId))
        {
            ticket = await CreateUploadTicketAsync("browser/upload.pdf");
        }

        ticket.Url.ShouldStartWith(DriveTicketUrl.UploadPath + "?ticket=");
        ticket.Url.ShouldNotContain("__tenant");

        using (var response = await PostFileAsync(ticket.Url, bytes, "upload.pdf"))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).ShouldContain("application/pdf");
        }

        using (_currentTenant.Change(DriveHttpApiTestModule.TenantId))
        {
            var file = await FindInDriveAsync("browser/upload.pdf");
            file.ShouldNotBeNull();
            file!.ClientId.ShouldBe(DriveHttpApiTestModule.AllowedClient);

            using (TestCaller.As(DriveHttpApiTestModule.AllowedClient))
            {
                (await _blobContainerFactory.Create(DriveHttpApiTestModule.AppTicketsContainer).GetAllBytesAsync("browser/upload.pdf"))
                    .ShouldBe(bytes);
            }
        }

        using (var again = await PostFileAsync(ticket.Url, bytes, "upload.pdf"))
        {
            again.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await again.Content.ReadAsStringAsync()).ShouldContain(DriveErrorCodes.InvalidTicket);
        }
    }

    [Fact]
    public async Task A_Failed_Upload_Should_Not_Use_The_Ticket_And_The_Tickets_Cap_Should_Hold()
    {
        var ticket = await CreateUploadTicketAsync("capped.pdf", maxSizeInBytes: 200);

        using (var tooLarge = await PostFileAsync(ticket.Url, DriveTestFiles.Pdf(new string('x', 400)), "capped.pdf"))
        {
            tooLarge.IsSuccessStatusCode.ShouldBeFalse();
            (await tooLarge.Content.ReadAsStringAsync()).ShouldContain("ContentTooLarge");
        }

        using (var fits = await PostFileAsync(ticket.Url, DriveTestFiles.Pdf("small"), "capped.pdf"))
        {
            fits.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task A_Browser_Should_Download_With_A_Ticket_Until_It_Expires()
    {
        var bytes = DriveTestFiles.Pdf("download " + Guid.NewGuid());
        using (TestCaller.As(DriveHttpApiTestModule.AllowedClient))
        {
            await _blobContainerFactory.Create(DriveHttpApiTestModule.AppTicketsContainer).SaveAsync("docs/report.pdf", bytes);
        }

        DriveTicketDto ticket;
        using (TestCaller.As(DriveHttpApiTestModule.AllowedClient))
        {
            ticket = await _ticketClient.CreateDownloadTicketAsync(DriveHttpApiTestModule.AppTicketsContainer, "docs/report.pdf");
        }

        for (var i = 0; i < 2; i++)
        {
            using var response = await Client.GetAsync(ticket.Url);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
            response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
            response.Content.Headers.ContentDisposition.FileNameStar.ShouldBe("report.pdf");
            response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        }

        GetRequiredService<ShiftableClock>().Offset = TimeSpan.FromSeconds(DriveTicketConsts.DefaultLifetimeSeconds + 1);
        (await Client.GetAsync(ticket.Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_Download_Ticket_For_A_Missing_File_Should_Be_404()
    {
        DriveTicketDto ticket;
        using (TestCaller.As(DriveHttpApiTestModule.AllowedClient))
        {
            ticket = await _ticketClient.CreateDownloadTicketAsync(DriveHttpApiTestModule.AppTicketsContainer, "missing.pdf");
        }

        (await Client.GetAsync(ticket.Url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_Ticket_Of_The_Other_Kind_Or_Tampered_With_Should_Be_Refused()
    {
        var upload = await CreateUploadTicketAsync("kind.pdf");

        (await Client.GetAsync(DriveTicketUrl.Build(upload: false, upload.Ticket))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var tampered = upload.Ticket.Substring(0, upload.Ticket.Length - 4) + (upload.Ticket.EndsWith("AAAA") ? "BBBB" : "AAAA");
        using var response = await PostFileAsync(DriveTicketUrl.Build(upload: true, tampered), DriveTestFiles.Pdf("x"), "kind.pdf");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Redemption_Should_Check_The_Issuer_Again()
    {
        var ticket = await CreateUploadTicketAsync("recheck.pdf");

        // The application loses the container after the ticket was issued.
        GetRequiredService<IOptions<AbpBlobStoringOptions>>().Value.Containers
            .GetConfiguration(DriveHttpApiTestModule.TicketsContainer)
            .GetDriveContainerConfiguration().AllowedClientIds = new[] { "another-app" };

        using var response = await PostFileAsync(ticket.Url, DriveTestFiles.Pdf("recheck"), "recheck.pdf");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Only_An_Allowed_Client_Without_A_User_Should_Get_A_Ticket()
    {
        using (TestCaller.As("another-app"))
        {
            var refused = await Should.ThrowAsync<AbpRemoteCallException>(() =>
                _ticketClient.CreateUploadTicketAsync(DriveHttpApiTestModule.AppTicketsContainer, "x.pdf"));
            refused.HttpStatusCode.ShouldBe((int)HttpStatusCode.Forbidden);
        }

        using (TestCaller.As(DriveHttpApiTestModule.AllowedClient, Guid.NewGuid()))
        {
            var refused = await Should.ThrowAsync<AbpRemoteCallException>(() =>
                _ticketClient.CreateUploadTicketAsync(DriveHttpApiTestModule.AppTicketsContainer, "x.pdf"));
            refused.HttpStatusCode.ShouldBe((int)HttpStatusCode.Forbidden);
        }
    }

    private async Task<DriveTicketDto> CreateUploadTicketAsync(string blobName, long? maxSizeInBytes = null)
    {
        using (TestCaller.As(DriveHttpApiTestModule.AllowedClient))
        {
            return await _ticketClient.CreateUploadTicketAsync(DriveHttpApiTestModule.AppTicketsContainer, blobName, maxSizeInBytes);
        }
    }

    private Task<HttpResponseMessage> PostFileAsync(string url, byte[] bytes, string fileName)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var form = new MultipartFormDataContent { { file, "content", fileName } };
        return Client.PostAsync(url, form);
    }

    private async Task<FileDescriptor?> FindInDriveAsync(string name)
    {
        using var uow = GetRequiredService<IUnitOfWorkManager>().Begin();
        var file = await GetRequiredService<FileDescriptorManager>().FindAsync(DriveHttpApiTestModule.TicketsContainer, name);
        await uow.CompleteAsync();
        return file;
    }
}
