using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobStoring.Pipeline;
using Shouldly;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Http.Client;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// An application issues an upload ticket as an OAuth client, for its signed-in user; the browser uploads with it,
/// anonymously, to Blob Management's own address; the application checks the uploader and confirms the file.
/// </summary>
public class BlobManagementUploadApi_Tests : AbpWebApplicationFactoryIntegratedTest<Program>
{
    private const string Container = BlobManagementHttpApiTestModule.OpenContainer;

    private readonly IBlobManagementFileService _appFiles;
    private readonly BlobManagementFileIntegrationClientProxy _files;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    private readonly Guid _appUser = Guid.NewGuid();

    public BlobManagementUploadApi_Tests()
    {
        _appFiles = GetRequiredService<IBlobManagementFileService>();
        _files = GetRequiredService<BlobManagementFileIntegrationClientProxy>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task A_Browser_Should_Upload_With_A_Ticket_And_The_Application_Confirm_It()
    {
        BlobManagementUploadTicketDto ticket;
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        using (AsApplicationUser(_appUser))
        {
            ticket = await _appFiles.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container });
        }

        ticket.UploadUrl.ShouldStartWith("/api/blob-management/uploads/");

        // The browser: no token, no user, no tenant header.
        var bytes = BlobManagementTestFiles.Pdf("browser " + Guid.NewGuid());
        using (var response = await PostFileAsync(ticket.UploadUrl, "invoice.pdf", bytes))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            json.RootElement.GetProperty("id").GetGuid().ShouldBe(ticket.FileId);
            json.RootElement.GetProperty("isPending").GetBoolean().ShouldBeTrue();
        }

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            var pending = await _appFiles.GetAsync(ticket.FileId);
            pending.IsPending.ShouldBeTrue();
            // Who uploaded it is what the application checks against its own user before it confirms.
            pending.CreatorId.ShouldBe(_appUser);
            pending.ClientId.ShouldBe(BlobManagementHttpApiTestModule.AllowedClient);
            pending.Name.ShouldBe("invoice.pdf");

            (await _appFiles.ConfirmAsync(ticket.FileId)).IsPending.ShouldBeFalse();
        }

        // Used once.
        using (var again = await PostFileAsync(ticket.UploadUrl, "again.pdf", BlobManagementTestFiles.Pdf("again " + Guid.NewGuid())))
        {
            again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
    }

    [Fact]
    public async Task The_Ticket_Should_Name_The_Tenant_And_The_Raw_Body_Should_Be_Taken_As_Well()
    {
        BlobManagementUploadTicketDto ticket;
        using (_currentTenant.Change(BlobManagementHttpApiTestModule.TenantId))
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            ticket = await _appFiles.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container });
        }

        using (var body = new ByteArrayContent(BlobManagementTestFiles.Pdf("raw " + Guid.NewGuid())))
        {
            body.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            using var response = await Client.PostAsync(ticket.UploadUrl + "?name=raw.pdf", body);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            using (_currentTenant.Change(BlobManagementHttpApiTestModule.TenantId))
            {
                (await _files.GetAsync(ticket.FileId)).Name.ShouldBe("raw.pdf");
            }

            // Not the host's.
            (await Should.ThrowAsync<AbpRemoteCallException>(() => _files.GetAsync(ticket.FileId)))
                .HttpStatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task A_Ticket_Should_Be_Issued_To_A_Granted_Client_Only_And_Checked_On_Upload()
    {
        using (TestCaller.As("another-app"))
        {
            (await Should.ThrowAsync<AbpRemoteCallException>(() =>
                    _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container })))
                .HttpStatusCode.ShouldBe((int)HttpStatusCode.Forbidden);
        }

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient, Guid.NewGuid()))
        {
            (await Should.ThrowAsync<AbpRemoteCallException>(() =>
                    _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container })))
                .HttpStatusCode.ShouldBe((int)HttpStatusCode.Forbidden);
        }

        using (var forged = await PostFileAsync("/api/blob-management/uploads/forged-ticket", "a.pdf", BlobManagementTestFiles.Pdf("forged")))
        {
            forged.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        BlobManagementUploadTicketDto small;
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            small = await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container, MaxSize = 32 });
        }

        using (var tooLarge = await PostFileAsync(small.UploadUrl, "big.pdf", BlobManagementTestFiles.Pdf("larger than thirty-two bytes " + Guid.NewGuid())))
        {
            tooLarge.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        }
    }

    /// <summary>
    /// What the container's pipeline refuses is the request's content, not the caller's rights: 413, 415 or 400 with the
    /// pipeline's own error code, never ABP's default 403 for a business exception.
    /// </summary>
    [Fact]
    public async Task Refused_Content_Should_Answer_The_Status_Of_What_Is_Wrong_With_It()
    {
        BlobManagementUploadTicketDto picturesOnly, disguised;
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            picturesOnly = await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput
            {
                ContainerName = Container,
                AllowedContentTypes = { "image/png" }
            });
            disguised = await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container });
        }

        using (var notAllowed = await PostFileAsync(picturesOnly.UploadUrl, "letter.pdf", BlobManagementTestFiles.Pdf("not a picture " + Guid.NewGuid())))
        {
            notAllowed.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
            (await notAllowed.Content.ReadAsStringAsync()).ShouldContain(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed);
        }

        using (var mismatch = await PostFileAsync(disguised.UploadUrl, "photo.png", BlobManagementTestFiles.Pdf("a pdf named png " + Guid.NewGuid())))
        {
            mismatch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await mismatch.Content.ReadAsStringAsync()).ShouldContain(BlobStoringPipelineErrorCodes.ContentTypeMismatch);
        }
    }

    /// <summary>
    /// The HttpApi layer maps blob-storing's codes by their strings, since it does not reference blob-storing's packages:
    /// this pins those strings to blob-storing's constants.
    /// </summary>
    [Fact]
    public void BlobStorings_Codes_Should_Be_Mapped_By_Their_Published_Names()
    {
        var mappings = GetRequiredService<Microsoft.Extensions.Options.IOptions<Volo.Abp.AspNetCore.ExceptionHandling.AbpExceptionHttpStatusCodeOptions>>()
            .Value.ErrorCodeToHttpStatusCodeMappings;

        mappings[BlobStoringPipelineErrorCodes.ContentTooLarge].ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        mappings[BlobStoringPipelineErrorCodes.ContentTypeNotAllowed].ShouldBe(HttpStatusCode.UnsupportedMediaType);
        mappings[BlobStoringPipelineErrorCodes.ContentTypeMismatch].ShouldBe(HttpStatusCode.BadRequest);
        mappings[Dignite.Abp.BlobStoring.Imaging.BlobStoringImagingErrorCodes.ImageTooLarge].ShouldBe(HttpStatusCode.BadRequest);
        mappings[Dignite.Abp.BlobStoring.Imaging.BlobStoringImagingErrorCodes.ImageTooSmall].ShouldBe(HttpStatusCode.BadRequest);
        mappings[Dignite.Abp.BlobStoring.Imaging.BlobStoringImagingErrorCodes.ImageProcessingFailed].ShouldBe(HttpStatusCode.UnprocessableEntity);
        mappings[Dignite.Abp.BlobStoring.Imaging.BlobStoringImagingErrorCodes.ImageDecodeTimeout].ShouldBe(HttpStatusCode.ServiceUnavailable);

        // Over the allocation keeps ABP's 403: a 5xx would be retried by HTTP clients' resilience handlers.
        mappings.ShouldNotContainKey(BlobManagementErrorCodes.StorageQuotaExceeded);
    }

    /// <summary>
    /// The browser sends the SHA-256 of the file with the upload and the module checks it before it stores anything: a
    /// different one is a 400 that leaves the ticket unused, so the same ticket takes the right one next.
    /// </summary>
    [Fact]
    public async Task A_Checksum_Sent_As_A_Form_Field_Should_Be_Verified_Before_The_Ticket_Is_Used()
    {
        var ticket = await IssueTicketAsync();
        var bytes = BlobManagementTestFiles.Pdf("form checksum " + Guid.NewGuid());

        using (var wrong = await PostFileAsync(ticket.UploadUrl, "a.pdf", bytes, Sha256(BlobManagementTestFiles.Pdf("another file"))))
        {
            wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await wrong.Content.ReadAsStringAsync()).ShouldContain(BlobManagementErrorCodes.ChecksumMismatch);
        }

        using (var unreadable = await PostFileAsync(ticket.UploadUrl, "a.pdf", bytes, "not base64!"))
        {
            unreadable.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await unreadable.Content.ReadAsStringAsync()).ShouldContain(BlobManagementErrorCodes.ChecksumInvalid);
        }

        using (var right = await PostFileAsync(ticket.UploadUrl, "a.pdf", bytes, Sha256(bytes)))
        {
            right.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    /// <summary>
    /// A raw body is the file, so RFC 9530's <c>Content-Digest</c> is its checksum; a multipart body is not, so the
    /// header is not read there.
    /// </summary>
    [Fact]
    public async Task A_Content_Digest_Header_Should_Be_Verified_For_A_Raw_Body()
    {
        var ticket = await IssueTicketAsync();
        var bytes = BlobManagementTestFiles.Pdf("raw digest " + Guid.NewGuid());

        using (var wrong = await PostRawAsync(ticket.UploadUrl, bytes, "sha-256=:" + Sha256(BlobManagementTestFiles.Pdf("another file")) + ":"))
        {
            wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await wrong.Content.ReadAsStringAsync()).ShouldContain(BlobManagementErrorCodes.ChecksumMismatch);
        }

        // Only an algorithm this module does not check: refused, not ignored.
        using (var md5 = await PostRawAsync(ticket.UploadUrl, bytes, "md5=:" + Convert.ToBase64String(MD5.HashData(bytes)) + ":"))
        {
            md5.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await md5.Content.ReadAsStringAsync()).ShouldContain(BlobManagementErrorCodes.ChecksumInvalid);
        }

        // Other members of the dictionary are ignored; the sha-256 one decides.
        using (var right = await PostRawAsync(ticket.UploadUrl, bytes, "sha-512=:" + Convert.ToBase64String(SHA512.HashData(bytes)) + ":, sha-256=:" + Sha256(bytes) + ":"))
        {
            right.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    /// <summary>The box an application asks a ticket for over HTTP is the one the browser's picture is stored within.</summary>
    [Fact]
    public async Task A_Ticket_Issued_Over_Http_Should_Store_A_Picture_Within_Its_Box()
    {
        BlobManagementUploadTicketDto ticket;
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        using (AsApplicationUser(_appUser))
        {
            ticket = await _appFiles.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput
            {
                ContainerName = Container,
                ImageMaxHeight = 16
            });
        }

        using (var response = await PostFileAsync(ticket.UploadUrl, "wide.png", BlobManagementTestFiles.Png(256, 128, seed: 82)))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            using var content = await _appFiles.GetContentAsync(ticket.FileId);
            BlobManagementTestFiles.Measure(BlobManagementTestFiles.ReadAll(content.GetStream())).ShouldBe((32, 16));
        }
    }

    private async Task<BlobManagementUploadTicketDto> IssueTicketAsync()
    {
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        using (AsApplicationUser(_appUser))
        {
            return await _appFiles.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container });
        }
    }

    private static string Sha256(byte[] bytes) => Convert.ToBase64String(SHA256.HashData(bytes));

    private async Task<HttpResponseMessage> PostRawAsync(string url, byte[] bytes, string contentDigest)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url + "?name=raw.pdf") { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        request.Headers.TryAddWithoutValidation("Content-Digest", contentDigest);
        return await Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostFileAsync(string url, string name, byte[] bytes, string? sha256 = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", name);
        if (sha256 != null)
        {
            form.Add(new StringContent(sha256), "sha256");
        }

        return await Client.PostAsync(url, form);
    }

    private IDisposable AsApplicationUser(Guid userId)
    {
        return _principalAccessor.Change(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(AbpClaimTypes.UserId, userId.ToString()) }, "Application")));
    }
}
