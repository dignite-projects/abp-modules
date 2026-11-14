using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Links;
using Dignite.Abp.BlobManagement.Public;
using Dignite.Abp.BlobManagement.Tickets;
using Dignite.Abp.BlobManagement.Uploads;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// Read links, as an application in Blob Management's own process issues them: one address per file and window, the
/// anonymous read behind it, and what gets no link.
/// </summary>
public abstract class BlobManagementReadLinks_Tests<TStartupModule> : BlobManagementApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Container = BlobManagementTestContainers.Files;

    private readonly IBlobManagementFileService _files;
    private readonly IBlobManagementReadLinkAppService _links;

    protected BlobManagementReadLinks_Tests()
    {
        _files = GetRequiredService<IBlobManagementFileService>();
        _links = GetRequiredService<IBlobManagementReadLinkAppService>();
    }

    [Fact]
    public async Task A_File_Should_Be_Read_With_Its_Link_And_Get_The_Same_Link_Within_A_Window()
    {
        var bytes = BlobManagementTestFiles.Pdf("linked " + Guid.NewGuid());
        var file = await StoreAsync("contract.pdf", bytes);

        var link = await _files.CreateReadLinkAsync(file.Id, new CreateBlobManagementReadLinkInput());
        link.FileId.ShouldBe(file.Id);
        link.Url.ShouldStartWith("/api/blob-management/links/");

        var options = GetRequiredService<IOptions<BlobManagementReadLinkOptions>>().Value;
        link.ExpirationTime.Ticks.ShouldBe(link.ExpirationTime.Ticks / options.LinkWindow.Ticks * options.LinkWindow.Ticks);
        link.ExpirationTime.ShouldBeGreaterThanOrEqualTo(DateTime.UtcNow + options.LinkLifetime - TimeSpan.FromSeconds(5));
        link.ExpirationTime.ShouldBeLessThanOrEqualTo(DateTime.UtcNow + options.MaxLinkLifetime);

        // Asked again in the window: the same address, so a browser's cache serves it again.
        (await _files.CreateReadLinkAsync(file.Id, new CreateBlobManagementReadLinkInput())).Url.ShouldBe(link.Url);

        var token = Uri.UnescapeDataString(link.Url.Split('/').Last());
        var read = await _links.ReadAsync(token, new BlobManagementPublicReadRequest());
        read.File.Name.ShouldBe("contract.pdf");
        read.ExpirationTime.ShouldBe(link.ExpirationTime, TimeSpan.FromSeconds(1));
        (await ReadAsync(read.Content!)).ShouldBe(bytes);

        var current = await _links.ReadAsync(token, new BlobManagementPublicReadRequest { IfNoneMatch = { read.File.EntityTag } });
        current.NotModified.ShouldBeTrue();
        current.Content.ShouldBeNull();

        // Gone, the file is not found behind its link.
        await _files.DeleteAsync(file.Id);
        await Should.ThrowAsync<EntityNotFoundException>(() => _links.ReadAsync(token, new BlobManagementPublicReadRequest()));
    }

    [Fact]
    public async Task A_Lifetime_Should_Be_Kept_Under_The_Maximum()
    {
        var file = await StoreAsync("long.pdf", BlobManagementTestFiles.Pdf("long " + Guid.NewGuid()));
        var options = GetRequiredService<IOptions<BlobManagementReadLinkOptions>>().Value;

        var link = await _files.CreateReadLinkAsync(file.Id, new CreateBlobManagementReadLinkInput { LifetimeInSeconds = int.MaxValue });
        link.ExpirationTime.ShouldBeLessThanOrEqualTo(DateTime.UtcNow + options.MaxLinkLifetime);
        link.ExpirationTime.ShouldBeGreaterThan(DateTime.UtcNow + options.MaxLinkLifetime - options.LinkWindow - TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_Pending_File_Should_Get_No_Link()
    {
        var ticket = await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container });
        await GetRequiredService<IBlobManagementUploadAppService>().UploadAsync(ticket.Ticket,
            new RemoteStreamContent(new MemoryStream(BlobManagementTestFiles.Pdf("pending " + Guid.NewGuid())), "pending.pdf"));

        (await Should.ThrowAsync<BusinessException>(() => _files.CreateReadLinkAsync(ticket.FileId, new CreateBlobManagementReadLinkInput())))
            .Code.ShouldBe(BlobManagementErrorCodes.FilePending);

        var confirmed = await StoreAsync("a.pdf", BlobManagementTestFiles.Pdf("a " + Guid.NewGuid()));
        var links = await _files.CreateReadLinksAsync(new CreateBlobManagementReadLinksInput
        {
            Ids = { confirmed.Id, ticket.FileId, Guid.NewGuid(), confirmed.Id }
        });
        links.Items.Select(l => l.FileId).ShouldBe(new[] { confirmed.Id });

        await _files.ConfirmAsync(ticket.FileId);
        (await _files.CreateReadLinkAsync(ticket.FileId, new CreateBlobManagementReadLinkInput())).FileId.ShouldBe(ticket.FileId);
    }

    [Fact]
    public async Task A_Link_That_Is_Altered_Expired_Or_Of_Another_Kind_Should_Be_Refused()
    {
        var file = await StoreAsync("b.pdf", BlobManagementTestFiles.Pdf("b " + Guid.NewGuid()));
        var protector = GetRequiredService<BlobManagementTicketProtector>();

        var expired = protector.Protect(BlobManagementReadLink.Purpose, new BlobManagementReadLink { FileId = file.Id },
            DateTimeOffset.UtcNow.AddMinutes(-1));
        // An upload ticket is no read link, though the same key ring signed it.
        var upload = (await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container })).Ticket;

        foreach (var bad in new[] { expired, upload, "not-a-link" })
        {
            (await Should.ThrowAsync<BusinessException>(() => _links.ReadAsync(bad, new BlobManagementPublicReadRequest())))
                .Code.ShouldBe(BlobManagementErrorCodes.ReadLinkInvalid);
        }
    }

    private Task<BlobManagementFileDto> StoreAsync(string name, byte[] bytes)
    {
        return _files.CreateAsync(new CreateBlobManagementFileInput
        {
            ContainerName = Container,
            File = new RemoteStreamContent(new MemoryStream(bytes), name, "application/pdf")
        });
    }

    private static async Task<byte[]> ReadAsync(IRemoteStreamContent content)
    {
        using (content)
        {
            using var memory = new MemoryStream();
            await content.GetStream().CopyToAsync(memory);
            return memory.ToArray();
        }
    }
}
