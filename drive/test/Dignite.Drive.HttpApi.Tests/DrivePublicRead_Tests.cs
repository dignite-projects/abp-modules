using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Drive;
using Dignite.Drive.Public;
using Shouldly;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.BlobStoring;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Drive;

/// <summary>
/// Public reads over HTTP: an application stores through the remote provider, builds the address with
/// <see cref="IDrivePublicUrlBuilder"/>, and an anonymous visitor reads it - sized, cached, in the right tenant.
/// </summary>
public class DrivePublicRead_Tests : AbpWebApplicationFactoryIntegratedTest<Program>
{
    private readonly IBlobContainerFactory _blobContainerFactory;
    private readonly IDrivePublicUrlBuilder _urlBuilder;
    private readonly ICurrentTenant _currentTenant;

    public DrivePublicRead_Tests()
    {
        _blobContainerFactory = GetRequiredService<IBlobContainerFactory>();
        _urlBuilder = GetRequiredService<IDrivePublicUrlBuilder>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task An_Anonymous_Visitor_Should_Read_A_Public_File_At_Its_Address()
    {
        var bytes = DriveTestFiles.Png(40, 20, seed: 1);
        await StoreAsync("2026/11/photo.png", bytes);

        var url = _urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, "2026/11/photo.png");
        url.ShouldBe("/api/drive/public/public/2026/11/photo.png");

        using var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.CacheControl!.Public.ShouldBeTrue();
        response.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(1));
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task A_Size_Should_Resize_The_Picture()
    {
        await StoreAsync("wide.png", DriveTestFiles.Png(200, 100, seed: 2));

        var oneSide = await GetBytesAsync(_urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, "wide.png", width: 64));
        DriveTestFiles.Measure(oneSide).ShouldBe((64, 32));

        var box = await GetBytesAsync(_urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, "wide.png", width: 64, height: 64));
        DriveTestFiles.Measure(box).ShouldBe((64, 64));

        (await Client.GetAsync(_urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, "wide.png", width: DrivePublicUrl.MaxDimension + 1)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Never enlarged.
        var larger = await GetBytesAsync(_urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, "wide.png", width: 512));
        DriveTestFiles.Measure(larger).ShouldBe((200, 100));
    }

    [Theory]
    [InlineData(100, null)]
    [InlineData(null, 100)]
    [InlineData(64, 100)]
    [InlineData(1, null)]
    public async Task A_Size_Outside_The_Allowed_Sizes_Should_Be_400_For_GET_And_HEAD(int? width, int? height)
    {
        await StoreAsync("sizes.png", DriveTestFiles.Png(200, 100, seed: 7));
        var url = _urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, "sizes.png", width: width, height: height);

        using (var get = await Client.GetAsync(url))
        {
            get.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // A HEAD reads no bytes; it is refused the same way.
        using var head = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
        head.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Every_Size_Of_The_Default_Ladder_Should_Be_Served()
    {
        await StoreAsync("ladder.png", DriveTestFiles.Png(200, 100, seed: 8));

        foreach (var size in new DrivePublicReadOptions().AllowedSizes)
        {
            var url = _urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, "ladder.png", width: size);
            using var get = await Client.GetAsync(url);
            get.StatusCode.ShouldBe(HttpStatusCode.OK, "width " + size);

            using var head = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            head.StatusCode.ShouldBe(HttpStatusCode.OK, "HEAD width " + size);
        }
    }

    [Fact]
    public async Task A_Matching_ETag_Should_Be_304_Until_The_File_Changes()
    {
        await StoreAsync("cached.png", DriveTestFiles.Png(16, 16, seed: 3));
        var url = _urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, "cached.png");

        EntityTagHeaderValue etag;
        using (var first = await Client.GetAsync(url))
        {
            etag = first.Headers.ETag!;
        }

        using (var again = await GetAsync(url, etag))
        {
            again.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        }

        using (var sized = await Client.GetAsync(_urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, "cached.png", width: 64)))
        {
            sized.Headers.ETag.ShouldNotBe(etag);
        }

        await StoreAsync("cached.png", DriveTestFiles.Png(16, 16, seed: 4), overrideExisting: true);
        using (var changed = await GetAsync(url, etag))
        {
            changed.StatusCode.ShouldBe(HttpStatusCode.OK);
            changed.Headers.ETag.ShouldNotBe(etag);
        }
    }

    [Fact]
    public async Task One_Response_Should_Describe_One_File_When_The_Address_Is_Overwritten()
    {
        // No extension: the same address takes a picture first and a type outside the Drive's list after.
        var name = "swap-" + Guid.NewGuid();
        var url = _urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, name);
        var picture = DriveTestFiles.Png(12, 12, seed: 9);
        await StoreAsync(name, picture);

        EntityTagHeaderValue pictureTag;
        using (var first = await Client.GetAsync(url))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.OK);
            pictureTag = first.Headers.ETag!;
            first.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
            first.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
            (await first.Content.ReadAsByteArrayAsync()).ShouldBe(picture);
        }

        var binary = new byte[] { 0x00, 0x01, 0x02, 0x7F, 0x80, 0xFE, 0xFF, 0x11 };
        await StoreAsync(name, binary, overrideExisting: true);

        // The overwrite cannot be made to land inside one request from here (the application tests race it at the
        // lookup); this checks that every part of a response belongs to the same file. Before, the tag and the
        // disposition came from one lookup and the bytes and type from a second, so a response could mix the two.
        using var second = await GetAsync(url, pictureTag);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.Headers.ETag.ShouldNotBeNull();
        second.Headers.ETag.ShouldNotBe(pictureTag);
        second.Content.Headers.ContentType!.MediaType.ShouldBe("application/octet-stream");
        second.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        (await second.Content.ReadAsByteArrayAsync()).ShouldBe(binary);

        // And the new tag is the one that now answers 304.
        using var revalidated = await GetAsync(url, second.Headers.ETag!);
        revalidated.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task A_Weak_Or_Wildcard_If_None_Match_Should_Be_304()
    {
        await StoreAsync("weak.png", DriveTestFiles.Png(8, 8, seed: 10));
        var url = _urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, "weak.png");

        EntityTagHeaderValue etag;
        using (var first = await Client.GetAsync(url))
        {
            etag = first.Headers.ETag!;
        }

        using (var weak = await GetAsync(url, new EntityTagHeaderValue(etag.Tag, isWeak: true)))
        {
            weak.StatusCode.ShouldBe(HttpStatusCode.NotModified);
            weak.Headers.ETag.ShouldBe(etag);
        }

        using (var any = await GetAsync(url, EntityTagHeaderValue.Any))
        {
            any.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        }

        using (var other = await GetAsync(url, new EntityTagHeaderValue("\"other\"")))
        {
            other.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task HEAD_Should_Answer_Without_A_Body()
    {
        await StoreAsync("head.png", DriveTestFiles.Png(8, 8, seed: 5));

        using var response = await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Head, _urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, "head.png")));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_Address_Should_Name_The_Tenant_Of_An_Anonymous_Read()
    {
        var bytes = DriveTestFiles.Png(12, 12, seed: 6);
        string url;
        using (_currentTenant.Change(DriveHttpApiTestModule.TenantId))
        {
            await StoreAsync("tenant.png", bytes);
            url = _urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, "tenant.png");
        }

        url.ShouldContain("__tenant=" + DriveHttpApiTestModule.TenantId);
        (await GetBytesAsync(url)).ShouldBe(bytes);

        using var withoutTenant = await Client.GetAsync("/api/drive/public/public/tenant.png");
        withoutTenant.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_Type_Outside_The_Drives_List_Should_Be_Served_As_An_Attachment()
    {
        await StoreAsync("data.bin", new byte[] { 0x00, 0x01, 0x02, 0x7F, 0x80, 0xFE, 0xFF, 0x10 });

        using var response = await Client.GetAsync(_urlBuilder.Build(DriveHttpApiTestModule.AppPublicContainer, "data.bin"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
    }

    [Fact]
    public async Task Only_Public_Containers_Should_Be_Read()
    {
        using (TestCaller.As(DriveHttpApiTestModule.AllowedClient))
        {
            await _blobContainerFactory.Create(DriveHttpApiTestModule.AppContainer).SaveAsync("private.pdf", DriveTestFiles.Pdf("private"));
        }

        (await Client.GetAsync("/api/drive/public/files/private.pdf")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetAsync("/api/drive/public/public/missing.png")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetAsync("/api/drive/public/undeclared/any.png")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task StoreAsync(string name, byte[] bytes, bool overrideExisting = false)
    {
        using (TestCaller.As(DriveHttpApiTestModule.AllowedClient))
        {
            await _blobContainerFactory.Create(DriveHttpApiTestModule.AppPublicContainer).SaveAsync(name, bytes, overrideExisting);
        }
    }

    private async Task<byte[]> GetBytesAsync(string url)
    {
        using var response = await Client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private Task<HttpResponseMessage> GetAsync(string url, EntityTagHeaderValue ifNoneMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.IfNoneMatch.Add(ifNoneMatch);
        return Client.SendAsync(request);
    }
}
