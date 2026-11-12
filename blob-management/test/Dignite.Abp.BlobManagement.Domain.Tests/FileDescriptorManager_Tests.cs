using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The storage core end to end: bytes through <c>IBlobContainer</c> and its pipeline contributors, rows through
/// <see cref="FileDescriptorManager"/> and the database provider of the concrete test class.
/// </summary>
public abstract class FileDescriptorManager_Tests<TStartupModule> : BlobManagementDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly IFileDescriptorRepository _fileDescriptorRepository;
    private readonly IBlobContainerFactory _blobContainerFactory;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected FileDescriptorManager_Tests()
    {
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _fileDescriptorRepository = GetRequiredService<IFileDescriptorRepository>();
        _blobContainerFactory = GetRequiredService<IBlobContainerFactory>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    [Fact]
    public async Task Should_Store_And_Read_Back_Under_The_Callers_Name_Tenant_Scoped()
    {
        var bytes = BlobManagementTestFiles.Pdf("tenant scoped");
        var tenantId = Guid.NewGuid();

        using (_currentTenant.Change(tenantId))
        {
            var descriptor = await CreateAsync(BlobManagementTestContainers.Files, "2026/11/notes.pdf", bytes);

            descriptor.TenantId.ShouldBe(tenantId);
            descriptor.ContainerName.ShouldBe(BlobManagementTestContainers.Files);
            descriptor.Name.ShouldBe("2026/11/notes.pdf");
            Regex.IsMatch(descriptor.BlobName, "^[0-9a-f]{32}\\.pdf$").ShouldBeTrue();
            descriptor.MimeType.ShouldBe("application/pdf");
            descriptor.Size.ShouldBe(bytes.Length);
            descriptor.Hash.Length.ShouldBe(64);
            descriptor.OwnsBlob().ShouldBeTrue();

            var found = await FindAsync(BlobManagementTestContainers.Files, "2026/11/notes.pdf");
            found.ShouldNotBeNull();
            found!.Id.ShouldBe(descriptor.Id);
            (await ReadAsync(found)).ShouldBe(bytes);
        }

        // Another tenant does not see the row, however it addresses it.
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            (await FindAsync(BlobManagementTestContainers.Files, "2026/11/notes.pdf")).ShouldBeNull();
        }
    }

    [Fact]
    public async Task Should_Record_The_Writing_Client()
    {
        var descriptor = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Pdf("client " + Guid.NewGuid()));
            return await _fileDescriptorManager.CreateAsync(BlobManagementTestContainers.Files, "client.pdf", stream, clientId: "site-app");
        });

        descriptor.ClientId.ShouldBe("site-app");
    }

    [Fact]
    public async Task Should_Refuse_A_Container_The_BlobManagement_Did_Not_Declare()
    {
        var create = await Should.ThrowAsync<BusinessException>(() =>
            CreateAsync(BlobManagementTestContainers.NotManaged, "notes.pdf", BlobManagementTestFiles.Pdf()));
        create.Code.ShouldBe(BlobManagementErrorCodes.ContainerNotFound);
        create.Data["ContainerName"].ShouldBe(BlobManagementTestContainers.NotManaged);

        var find = await Should.ThrowAsync<BusinessException>(() => FindAsync(BlobManagementTestContainers.NotManaged, "notes.pdf"));
        find.Code.ShouldBe(BlobManagementErrorCodes.ContainerNotFound);
    }

    [Fact]
    public async Task Should_Refuse_An_Existing_Name_Unless_Overriding()
    {
        var name = "existing-" + Guid.NewGuid().ToString("N") + ".pdf";
        await CreateAsync(BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("first " + name));

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            CreateAsync(BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("second " + name)));
        exception.Code.ShouldBe(BlobManagementErrorCodes.BlobAlreadyExists);
    }

    [Fact]
    public async Task Overriding_Should_Replace_The_Descriptor_And_Remove_The_Old_Blob()
    {
        var name = "replaced-" + Guid.NewGuid().ToString("N") + ".pdf";
        var first = await CreateAsync(BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("first " + name));
        var secondBytes = BlobManagementTestFiles.Pdf("second " + name);

        var second = await CreateAsync(BlobManagementTestContainers.Files, name, secondBytes, overrideExisting: true);

        second.Id.ShouldNotBe(first.Id);
        second.BlobName.ShouldNotBe(first.BlobName);
        (await FindAsync(BlobManagementTestContainers.Files, name))!.Id.ShouldBe(second.Id);
        (await ReadAsync(second)).ShouldBe(secondBytes);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, first.BlobName)).ShouldBeFalse();
        await ShouldBeTheOnlyLiveHolderOfTheNameAsync(second, first);
    }

    [Fact]
    public async Task Overriding_With_The_Same_Bytes_Should_Keep_The_Blob()
    {
        var name = "same-" + Guid.NewGuid().ToString("N") + ".pdf";
        var bytes = BlobManagementTestFiles.Pdf(name);
        var first = await CreateAsync(BlobManagementTestContainers.Files, name, bytes);

        // One unit of work takes the dedup lock for the upload and needs it again for the replaced descriptor's BLOB
        // after commit: it reuses it rather than waiting for itself.
        var second = await CreateAsync(BlobManagementTestContainers.Files, name, bytes, overrideExisting: true);

        second.BlobName.ShouldBe(first.BlobName);
        (await ReadAsync(second)).ShouldBe(bytes);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, second.BlobName)).ShouldBeTrue();
        await ShouldBeTheOnlyLiveHolderOfTheNameAsync(second, first);
    }

    /// <summary>
    /// The lookup by name is check-then-act; the name key is the arbiter. A second live descriptor under the same name -
    /// inserted past the manager's check, as the loser of two simultaneous saves would be - is refused by the database,
    /// and the name is free again once its holder is deleted.
    /// </summary>
    [Fact]
    public async Task A_Name_Should_Have_One_Live_Descriptor_In_A_System_Container()
    {
        var name = "unique-" + Guid.NewGuid().ToString("N") + ".pdf";
        var first = await CreateAsync(BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("first " + name));
        first.NameKey.ShouldBe(FileDescriptor.CreateNameKey(null, BlobManagementTestContainers.Files, name));

        await Should.ThrowAsync<Exception>(() => WithUnitOfWorkAsync(() =>
            _fileDescriptorRepository.InsertAsync(
                new FileDescriptor(
                    Guid.NewGuid(), BlobManagementTestContainers.Files, name, Guid.NewGuid().ToString("N") + ".pdf",
                    "application/pdf", 1, new string('0', 64), ownsBlob: false, nameIsAddress: true),
                autoSave: true)));

        await WithUnitOfWorkAsync(async () =>
            await _fileDescriptorManager.DeleteAsync(await _fileDescriptorRepository.GetAsync(first.Id)));

        var again = await CreateAsync(BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("again " + name));
        again.NameKey.ShouldBe(first.NameKey);
        (await FindAsync(BlobManagementTestContainers.Files, name))!.Id.ShouldBe(again.Id);
    }

    /// <summary>
    /// A unit of work that stores the same bytes twice holds the dedup lock after the first upload and reuses it for
    /// the second, which shares the first one's BLOB.
    /// </summary>
    [Fact]
    public async Task The_Same_Bytes_Twice_In_One_Unit_Of_Work_Should_Share_One_Blob()
    {
        var bytes = BlobManagementTestFiles.Pdf("one unit of work " + Guid.NewGuid());

        var (first, second) = await WithUnitOfWorkAsync(async () =>
        {
            using var firstStream = new MemoryStream(bytes);
            var a = await _fileDescriptorManager.CreateAsync(BlobManagementTestContainers.Files, "a-" + Guid.NewGuid().ToString("N") + ".pdf", firstStream);
            using var secondStream = new MemoryStream(bytes);
            var b = await _fileDescriptorManager.CreateAsync(BlobManagementTestContainers.Files, "b-" + Guid.NewGuid().ToString("N") + ".pdf", secondStream);
            return (a, b);
        });

        second.BlobName.ShouldBe(first.BlobName);
        first.OwnsBlob().ShouldBeTrue();
        second.OwnsBlob().ShouldBeFalse();
    }

    /// <summary>
    /// The BLOB is deleted after commit: a unit of work that deletes the descriptor and then rolls back leaves the
    /// bytes in place. (The test database runs without transactions, so the row itself stays deleted here; with
    /// transactions the rollback restores it, and its bytes are still there.)
    /// </summary>
    [Fact]
    public async Task A_Rolled_Back_Delete_Should_Keep_The_Blob()
    {
        var bytes = BlobManagementTestFiles.Pdf("rolled back " + Guid.NewGuid());
        var file = await CreateAsync(BlobManagementTestContainers.Files, "rolled-back-" + Guid.NewGuid().ToString("N") + ".pdf", bytes);

        using (var scope = ServiceProvider.CreateScope())
        {
            var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
            using (var unitOfWork = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions()))
            {
                await _fileDescriptorManager.DeleteAsync(await _fileDescriptorRepository.GetAsync(file.Id));
                await unitOfWork.RollbackAsync();
            }
        }

        (await BlobExistsAsync(BlobManagementTestContainers.Files, file.BlobName)).ShouldBeTrue();
    }

    /// <summary>
    /// When the dedup lock cannot be had, the upload neither fails nor dedups: the bytes are stored again under a new
    /// BLOB name that no descriptor claims the dedup key for, and that BLOB goes with its descriptor.
    /// </summary>
    [Fact]
    public async Task Without_The_Dedup_Lock_An_Upload_Should_Be_Stored_Without_Dedup()
    {
        var bytes = BlobManagementTestFiles.Pdf("lockless " + Guid.NewGuid());
        var first = await CreateAsync(BlobManagementTestContainers.Files, "locked-" + Guid.NewGuid().ToString("N") + ".pdf", bytes);

        var lockless = GetRequiredService<LocklessFileDescriptorManager>();
        var second = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await lockless.CreateAsync(BlobManagementTestContainers.Files, "lockless-" + Guid.NewGuid().ToString("N") + ".pdf", stream);
        });

        second.DedupKey.ShouldBeNull();
        second.OwnsBlob().ShouldBeFalse();
        second.BlobName.ShouldNotBe(first.BlobName);
        second.NameKey.ShouldNotBeNull();
        (await ReadAsync(second)).ShouldBe(bytes);

        await WithUnitOfWorkAsync(async () =>
            await _fileDescriptorManager.DeleteAsync(await _fileDescriptorRepository.GetAsync(second.Id)));
        (await BlobExistsAsync(BlobManagementTestContainers.Files, second.BlobName)).ShouldBeFalse();
        (await BlobExistsAsync(BlobManagementTestContainers.Files, first.BlobName)).ShouldBeTrue();
    }

    /// <summary>
    /// The same bytes under two names keep one BLOB: the second descriptor shares the first one's, and the lookup runs
    /// before the save, so nothing is written for it.
    /// </summary>
    [Fact]
    public async Task Should_Dedup_Identical_Content_Into_One_Blob()
    {
        var bytes = BlobManagementTestFiles.Pdf("dedup " + Guid.NewGuid());

        var first = await CreateAsync(BlobManagementTestContainers.Files, "first.pdf", bytes);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, first.BlobName)).ShouldBeTrue();
        var second = await CreateAsync(BlobManagementTestContainers.Files, "second.pdf", bytes);

        second.BlobName.ShouldBe(first.BlobName);
        second.Hash.ShouldBe(first.Hash);
        second.Size.ShouldBe(first.Size);
        second.MimeType.ShouldBe(first.MimeType);
        first.OwnsBlob().ShouldBeTrue();
        second.OwnsBlob().ShouldBeFalse();
        (await ReadAsync(second)).ShouldBe(bytes);
    }

    [Fact]
    public async Task Should_Not_Dedup_Across_Tenants()
    {
        var bytes = BlobManagementTestFiles.Pdf("tenants " + Guid.NewGuid());

        FileDescriptor first, second;
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            first = await CreateAsync(BlobManagementTestContainers.Files, "a.pdf", bytes);
        }

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            second = await CreateAsync(BlobManagementTestContainers.Files, "a.pdf", bytes);
        }

        second.BlobName.ShouldNotBe(first.BlobName);
        second.OwnsBlob().ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_Should_Keep_A_Blob_While_Another_Descriptor_Uses_It()
    {
        var bytes = BlobManagementTestFiles.Pdf("shared " + Guid.NewGuid());
        var writer = await CreateAsync(BlobManagementTestContainers.Files, "writer.pdf", bytes);
        var sharer = await CreateAsync(BlobManagementTestContainers.Files, "sharer.pdf", bytes);

        // The writer goes first: its BLOB stays for the sharer.
        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(writer));
        (await BlobExistsAsync(BlobManagementTestContainers.Files, writer.BlobName)).ShouldBeTrue();
        (await ReadAsync(sharer)).ShouldBe(bytes);
        (await FindAsync(BlobManagementTestContainers.Files, "writer.pdf")).ShouldBeNull();

        // The last user goes: the BLOB goes with it.
        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(sharer));
        (await BlobExistsAsync(BlobManagementTestContainers.Files, writer.BlobName)).ShouldBeFalse();
    }

    /// <summary>
    /// A deleted writer gives up its dedup key, so the same bytes can be stored again once no live descriptor has
    /// them; the deleted row keeps its hash for the audit trail.
    /// </summary>
    [Fact]
    public async Task Should_Store_The_Same_Bytes_Again_After_They_Were_Deleted()
    {
        var bytes = BlobManagementTestFiles.Pdf("again " + Guid.NewGuid());
        var first = await CreateAsync(BlobManagementTestContainers.Files, "first.pdf", bytes);
        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(first));

        var second = await CreateAsync(BlobManagementTestContainers.Files, "again.pdf", bytes);

        second.OwnsBlob().ShouldBeTrue();
        second.BlobName.ShouldNotBe(first.BlobName);
        (await ReadAsync(second)).ShouldBe(bytes);

        using (_dataFilter.Disable<ISoftDelete>())
        {
            var deleted = await WithUnitOfWorkAsync(() => _fileDescriptorRepository.GetAsync(first.Id));
            deleted.IsDeleted.ShouldBeTrue();
            deleted.Hash.ShouldBe(first.Hash);
            deleted.OwnsBlob().ShouldBeFalse();
        }
    }

    /// <summary>
    /// Two simultaneous uploads of the same bytes can both miss the dedup lookup. The unique dedup key lets only one of
    /// them keep its BLOB: the loser's insert fails and its BLOB is removed, and a retry shares the winner's.
    /// </summary>
    [Fact]
    public async Task The_Loser_Of_A_Dedup_Race_Should_Fail_And_Leave_No_Blob()
    {
        var bytes = BlobManagementTestFiles.Pdf("race " + Guid.NewGuid());
        var winner = await CreateAsync(BlobManagementTestContainers.Files, "winner.pdf", bytes);

        var racing = GetRequiredService<RacingFileDescriptorManager>();
        await Should.ThrowAsync<Exception>(() => WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await racing.CreateAsync(BlobManagementTestContainers.Files, "loser.pdf", stream);
        }));

        racing.GeneratedBlobNames.Count.ShouldBe(1);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, racing.GeneratedBlobNames[0])).ShouldBeFalse();
        (await FindAsync(BlobManagementTestContainers.Files, "loser.pdf")).ShouldBeNull();

        var retry = await CreateAsync(BlobManagementTestContainers.Files, "loser.pdf", bytes);
        retry.BlobName.ShouldBe(winner.BlobName);
        (await ReadAsync(winner)).ShouldBe(bytes);
    }

    [Fact]
    public async Task Should_Refuse_Content_Its_Extension_Contradicts_And_Store_Nothing()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            CreateAsync(BlobManagementTestContainers.Files, "picture.png", BlobManagementTestFiles.Pdf("not a picture")));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeMismatch);
        (await FindAsync(BlobManagementTestContainers.Files, "picture.png")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Run_The_Containers_Contributors()
    {
        var tooLarge = await Should.ThrowAsync<BusinessException>(() =>
            CreateAsync(BlobManagementTestContainers.Limited, "large.pdf", BlobManagementTestFiles.Pdf(new string('x', (int)BlobManagementTestContainers.LimitedMaxSizeInBytes))));
        tooLarge.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);

        var notAllowed = await Should.ThrowAsync<BusinessException>(() =>
            CreateAsync(BlobManagementTestContainers.Limited, "notes.txt", BlobManagementTestFiles.Text("not a pdf")));
        notAllowed.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed);
        (await FindAsync(BlobManagementTestContainers.Limited, "notes.txt")).ShouldBeNull();

        var allowed = await CreateAsync(BlobManagementTestContainers.Limited, "small.pdf", BlobManagementTestFiles.Pdf("small"));
        allowed.MimeType.ShouldBe("application/pdf");
    }

    [Fact]
    public async Task A_Name_Without_An_Extension_Is_Typed_From_The_Content()
    {
        var descriptor = await CreateAsync(BlobManagementTestContainers.Files, "no-extension-" + Guid.NewGuid().ToString("N"), BlobManagementTestFiles.Pdf("typed"));

        descriptor.MimeType.ShouldBe("application/pdf");
        descriptor.BlobName.ShouldEndWith(".pdf");
    }

    private Task<FileDescriptor> CreateAsync(string containerName, string name, byte[] bytes, bool overrideExisting = false)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _fileDescriptorManager.CreateAsync(containerName, name, stream, overrideExisting);
        });
    }

    private Task<FileDescriptor?> FindAsync(string containerName, string name)
    {
        return WithUnitOfWorkAsync(() => _fileDescriptorManager.FindAsync(containerName, name));
    }

    private Task<byte[]> ReadAsync(FileDescriptor descriptor)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            await using var stream = await _fileDescriptorManager.GetStreamOrNullAsync(descriptor);
            stream.ShouldNotBeNull();
            using var memory = new MemoryStream();
            await stream!.CopyToAsync(memory);
            return memory.ToArray();
        });
    }

    private Task<bool> BlobExistsAsync(string containerName, string blobName)
    {
        return _blobContainerFactory.Create(containerName).ExistsAsync(blobName);
    }

    /// <summary>
    /// <paramref name="holder"/> is the one live descriptor with its name, and holds the name key;
    /// <paramref name="replaced"/>, deleted, holds neither key.
    /// </summary>
    private async Task ShouldBeTheOnlyLiveHolderOfTheNameAsync(FileDescriptor holder, FileDescriptor replaced)
    {
        var live = (await WithUnitOfWorkAsync(() => _fileDescriptorRepository.GetListAsync()))
            .Where(f => f.ContainerName == holder.ContainerName && f.Name == holder.Name)
            .ToList();
        live.Count.ShouldBe(1);
        live[0].Id.ShouldBe(holder.Id);
        live[0].NameKey.ShouldBe(FileDescriptor.CreateNameKey(holder.TenantId, holder.ContainerName, holder.Name));

        using (_dataFilter.Disable<ISoftDelete>())
        {
            var deleted = await WithUnitOfWorkAsync(() => _fileDescriptorRepository.GetAsync(replaced.Id));
            deleted.IsDeleted.ShouldBeTrue();
            deleted.NameKey.ShouldBeNull();
            deleted.DedupKey.ShouldBeNull();
        }
    }
}
