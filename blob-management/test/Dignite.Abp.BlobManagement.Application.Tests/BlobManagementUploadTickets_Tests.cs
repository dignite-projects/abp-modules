using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Public;
using Dignite.Abp.BlobManagement.Tickets;
using Dignite.Abp.BlobManagement.Uploads;
using Dignite.Abp.BlobStoring.Pipeline;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Timing;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// A browser's upload with a ticket, as an application in Blob Management's own process issues it: the ticket's limits,
/// the file pending until its business system confirms it, a ticket used once, and the cleanup of what is never
/// confirmed.
/// </summary>
public abstract class BlobManagementUploadTickets_Tests<TStartupModule> : BlobManagementApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Container = BlobManagementTestContainers.Files;

    private readonly IBlobManagementFileService _files;
    private readonly IBlobManagementUploadAppService _uploads;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private readonly Guid _user = Guid.NewGuid();

    protected BlobManagementUploadTickets_Tests()
    {
        _files = GetRequiredService<IBlobManagementFileService>();
        _uploads = GetRequiredService<IBlobManagementUploadAppService>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task A_File_Uploaded_With_A_Ticket_Should_Be_Pending_Until_It_Is_Confirmed()
    {
        BlobManagementUploadTicketDto ticket;
        using (_principalAccessor.As(_user))
        {
            ticket = await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container });
        }

        ticket.UploadUrl.ShouldBe(BlobManagementUploadUrl.Build(ticket.Ticket));
        ticket.ExpirationTime.ShouldBeGreaterThan(DateTime.UtcNow);

        var usedBefore = await UsedStorageAsync();
        var bytes = BlobManagementTestFiles.Pdf("ticket " + Guid.NewGuid());
        var uploaded = await _uploads.UploadAsync(ticket.Ticket, Content("scan.pdf", bytes));

        uploaded.Id.ShouldBe(ticket.FileId);
        uploaded.IsPending.ShouldBeTrue();
        // The user the ticket was issued for, which the business system checks before it confirms.
        uploaded.CreatorId.ShouldBe(_user);
        uploaded.ClientId.ShouldBeNull();

        // Not listed, but found by its id - and counted.
        (await ListAsync()).ShouldNotContain(ticket.FileId);
        var pending = await _files.GetAsync(ticket.FileId);
        pending.IsPending.ShouldBeTrue();
        pending.CreatorId.ShouldBe(_user);
        (await UsedStorageAsync()).ShouldBe(usedBefore + bytes.Length);

        (await _files.ConfirmAsync(ticket.FileId)).IsPending.ShouldBeFalse();
        (await ListAsync()).ShouldContain(ticket.FileId);
        // Confirming again changes nothing.
        (await _files.ConfirmAsync(ticket.FileId)).IsPending.ShouldBeFalse();
    }

    [Fact]
    public async Task A_Ticket_Should_Store_One_File_Once()
    {
        var ticket = await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container });
        await _uploads.UploadAsync(ticket.Ticket, Content("a.pdf", BlobManagementTestFiles.Pdf("once " + Guid.NewGuid())));

        var again = await Should.ThrowAsync<BusinessException>(() =>
            _uploads.UploadAsync(ticket.Ticket, Content("b.pdf", BlobManagementTestFiles.Pdf("twice " + Guid.NewGuid()))));
        again.Code.ShouldBe(BlobManagementErrorCodes.UploadTicketUsed);

        // Deleted, the file still keeps its ticket from being used again.
        await _files.DeleteAsync(ticket.FileId);
        (await Should.ThrowAsync<BusinessException>(() =>
                _uploads.UploadAsync(ticket.Ticket, Content("c.pdf", BlobManagementTestFiles.Pdf("thrice " + Guid.NewGuid())))))
            .Code.ShouldBe(BlobManagementErrorCodes.UploadTicketUsed);
    }

    [Fact]
    public async Task A_Ticket_That_Is_Altered_Expired_Or_Of_Another_Kind_Should_Be_Refused()
    {
        var ticket = await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container });
        var protector = GetRequiredService<BlobManagementTicketProtector>();

        var altered = ticket.Ticket[..^4] + (ticket.Ticket.EndsWith("AAAA") ? "BBBB" : "AAAA");
        var expired = protector.Protect(BlobManagementUploadTicket.Purpose,
            new BlobManagementUploadTicket { ContainerName = Container, FileId = Guid.NewGuid(), MaxSize = 1024 },
            DateTimeOffset.UtcNow.AddMinutes(-1));
        var otherKind = protector.Protect("Some.Other.Purpose",
            new BlobManagementUploadTicket { ContainerName = Container, FileId = Guid.NewGuid(), MaxSize = 1024 },
            DateTimeOffset.UtcNow.AddMinutes(5));

        foreach (var bad in new[] { altered, expired, otherKind, "not-a-ticket" })
        {
            (await Should.ThrowAsync<BusinessException>(() => _uploads.GetTicketAsync(bad)))
                .Code.ShouldBe(BlobManagementErrorCodes.UploadTicketInvalid);
            (await Should.ThrowAsync<BusinessException>(() => _uploads.UploadAsync(bad, Content("a.pdf", BlobManagementTestFiles.Pdf("bad")))))
                .Code.ShouldBe(BlobManagementErrorCodes.UploadTicketInvalid);
        }
    }

    [Fact]
    public async Task A_Ticket_Should_Hold_The_Upload_To_Its_Size_And_Types()
    {
        var small = await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container, MaxSize = 16 });
        (await _uploads.GetTicketAsync(small.Ticket)).MaxSize.ShouldBe(16);
        (await Should.ThrowAsync<BusinessException>(() =>
                _uploads.UploadAsync(small.Ticket, Content("big.pdf", BlobManagementTestFiles.Pdf("too large for the ticket")))))
            .Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);

        var pictures = new CreateBlobManagementUploadTicketInput { ContainerName = Container, AllowedContentTypes = { "image/*" } };
        var refused = await _files.CreateUploadTicketAsync(pictures);
        (await Should.ThrowAsync<BusinessException>(() =>
                _uploads.UploadAsync(refused.Ticket, Content("doc.pdf", BlobManagementTestFiles.Pdf("not a picture")))))
            .Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed);

        var accepted = await _files.CreateUploadTicketAsync(pictures);
        (await _uploads.UploadAsync(accepted.Ticket, Content("photo.png", BlobManagementTestFiles.Png(8, 8, seed: 31))))
            .MimeType.ShouldBe("image/png");

        // A size larger than the container takes is the container's.
        var limited = await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput
        {
            ContainerName = BlobManagementTestContainers.Limited,
            MaxSize = long.MaxValue
        });
        (await _uploads.GetTicketAsync(limited.Ticket)).MaxSize.ShouldBe(BlobManagementTestContainers.LimitedMaxSizeInBytes);
    }

    [Fact]
    public async Task A_Ticket_Should_Name_A_Place_That_Is_There()
    {
        await Should.ThrowAsync<BusinessException>(() =>
            _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = BlobManagementTestContainers.NotManaged }));
        await Should.ThrowAsync<EntityNotFoundException>(() =>
            _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput
            {
                ContainerName = BlobManagementTestContainers.Documents,
                DirectoryId = Guid.NewGuid()
            }));
    }

    [Fact]
    public async Task A_Pending_File_Should_Not_Be_Read_Publicly()
    {
        var ticket = await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container });
        await _uploads.UploadAsync(ticket.Ticket, Content("poster.pdf", BlobManagementTestFiles.Pdf("pending " + Guid.NewGuid())));
        var marked = await _files.SetPublicAsync(ticket.FileId, new SetBlobManagementFilePublicInput { IsPublic = true });

        var publicFiles = GetRequiredService<IBlobManagementPublicAppService>();
        var blobName = marked.Url!.Split('/').Last();
        await Should.ThrowAsync<EntityNotFoundException>(() => publicFiles.ReadAsync(
            Container, blobName, new BlobManagementPublicReadInput(), new BlobManagementPublicReadRequest { HeadersOnly = true }));

        await _files.ConfirmAsync(ticket.FileId);
        (await publicFiles.ReadAsync(Container, blobName, new BlobManagementPublicReadInput(), new BlobManagementPublicReadRequest { HeadersOnly = true }))
            .File.Name.ShouldBe("poster.pdf");
    }

    [Fact]
    public async Task The_Cleanup_Should_Delete_What_Was_Never_Confirmed()
    {
        var abandoned = await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container });
        await _uploads.UploadAsync(abandoned.Ticket, Content("left.pdf", BlobManagementTestFiles.Pdf("left " + Guid.NewGuid())));
        var kept = await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput { ContainerName = Container });
        await _uploads.UploadAsync(kept.Ticket, Content("kept.pdf", BlobManagementTestFiles.Pdf("kept " + Guid.NewGuid())));
        await _files.ConfirmAsync(kept.FileId);

        var cleaner = GetRequiredService<BlobManagementPendingFileCleaner>();

        // Not old enough yet.
        (await cleaner.CleanAsync()).ShouldBe(0);
        (await _files.GetAsync(abandoned.FileId)).IsPending.ShouldBeTrue();

        (await cleaner.CleanAsync(GetRequiredService<IClock>().Now.AddMinutes(1))).ShouldBeGreaterThanOrEqualTo(1);
        await Should.ThrowAsync<EntityNotFoundException>(() => _files.GetAsync(abandoned.FileId));
        (await _files.GetAsync(kept.FileId)).IsPending.ShouldBeFalse();
    }

    private async Task<Guid[]> ListAsync()
    {
        return (await _files.GetListAsync(new GetBlobManagementFilesInput { ContainerName = Container, MaxResultCount = 1000 }))
            .Items.Select(f => f.Id).ToArray();
    }

    private Task<long> UsedStorageAsync()
    {
        return WithUnitOfWorkAsync(() => GetRequiredService<IFileDescriptorRepository>().GetUsedStorageSizeAsync());
    }

    private static IRemoteStreamContent Content(string name, byte[] bytes)
    {
        return new RemoteStreamContent(new MemoryStream(bytes), name, "application/octet-stream");
    }
}
