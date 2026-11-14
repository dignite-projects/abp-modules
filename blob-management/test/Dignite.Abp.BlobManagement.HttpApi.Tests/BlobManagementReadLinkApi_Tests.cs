using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Files;
using Shouldly;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Content;
using Volo.Abp.Http.Client;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// An application asks for a read link as an OAuth client; the browser reads with it, anonymously, from Blob
/// Management's own address - privately cached, for no longer than the link lives.
/// </summary>
public class BlobManagementReadLinkApi_Tests : AbpWebApplicationFactoryIntegratedTest<Program>
{
    private const string Container = BlobManagementHttpApiTestModule.OpenContainer;

    private readonly BlobManagementFileClientProxy _files;

    public BlobManagementReadLinkApi_Tests()
    {
        _files = GetRequiredService<BlobManagementFileClientProxy>();
    }

    [Fact]
    public async Task A_Browser_Should_Read_A_File_With_Its_Link()
    {
        var bytes = BlobManagementTestFiles.Pdf("read link " + Guid.NewGuid());
        BlobManagementReadLinkDto link;
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            var file = await _files.CreateAsync(new CreateBlobManagementFileInput
            {
                ContainerName = Container,
                File = new RemoteStreamContent(new MemoryStream(bytes), "letter.pdf", "application/pdf")
            });
            link = await _files.CreateReadLinkAsync(file.Id, new CreateBlobManagementReadLinkInput());

            (await _files.CreateReadLinksAsync(new CreateBlobManagementReadLinksInput { Ids = { file.Id } }))
                .Items.Single().Url.ShouldBe(link.Url);
        }

        EntityTagHeaderValue entityTag;
        using (var response = await Client.GetAsync(link.Url))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
            response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
            response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
            response.Content.Headers.ContentDisposition.FileName.ShouldBe("letter.pdf");
            response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");

            var cacheControl = response.Headers.CacheControl!;
            cacheControl.Private.ShouldBeTrue();
            cacheControl.Public.ShouldBeFalse();
            cacheControl.MaxAge.ShouldNotBeNull();
            cacheControl.MaxAge!.Value.ShouldBeGreaterThan(TimeSpan.Zero);
            // Never longer than the link has left.
            cacheControl.MaxAge.Value.ShouldBeLessThanOrEqualTo(link.ExpirationTime - DateTime.UtcNow + TimeSpan.FromSeconds(1));

            entityTag = response.Headers.ETag!;
        }

        using (var request = new HttpRequestMessage(HttpMethod.Get, link.Url))
        {
            request.Headers.IfNoneMatch.Add(entityTag);
            using var notModified = await Client.SendAsync(request);
            notModified.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        }

        using (var head = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, link.Url)))
        {
            head.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await head.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
        }

        // A viewer reads a part, as it seeks.
        using (var request = new HttpRequestMessage(HttpMethod.Get, link.Url))
        {
            request.Headers.Range = new RangeHeaderValue(5, null);
            using var part = await Client.SendAsync(request);
            part.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
            part.Headers.CacheControl!.Private.ShouldBeTrue();
            (await part.Content.ReadAsByteArrayAsync()).ShouldBe(bytes[5..]);
        }

        (await Client.GetAsync("/api/blob-management/links/forged")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_Link_Should_Be_Issued_To_A_Granted_Client_Only()
    {
        BlobManagementFileDto file;
        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient))
        {
            file = await _files.CreateAsync(new CreateBlobManagementFileInput
            {
                ContainerName = Container,
                File = new RemoteStreamContent(new MemoryStream(BlobManagementTestFiles.Pdf("x " + Guid.NewGuid())), "x.pdf", "application/pdf")
            });
        }

        using (TestCaller.As("another-app"))
        {
            (await Should.ThrowAsync<AbpRemoteCallException>(() => _files.CreateReadLinkAsync(file.Id, new CreateBlobManagementReadLinkInput())))
                .HttpStatusCode.ShouldBe((int)HttpStatusCode.NotFound);
            (await _files.CreateReadLinksAsync(new CreateBlobManagementReadLinksInput { Ids = { file.Id } })).Items.ShouldBeEmpty();
        }

        using (TestCaller.As(BlobManagementHttpApiTestModule.AllowedClient, Guid.NewGuid()))
        {
            (await Should.ThrowAsync<AbpRemoteCallException>(() => _files.CreateReadLinkAsync(file.Id, new CreateBlobManagementReadLinkInput())))
                .HttpStatusCode.ShouldBe((int)HttpStatusCode.Forbidden);
        }
    }
}
