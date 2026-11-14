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
    public async Task Overriding_With_The_Same_Bytes_Should_Store_Them_Anew()
    {
        var name = "same-" + Guid.NewGuid().ToString("N") + ".pdf";
        var bytes = BlobManagementTestFiles.Pdf(name);
        var first = await CreateAsync(BlobManagementTestContainers.Files, name, bytes);

        var second = await CreateAsync(BlobManagementTestContainers.Files, name, bytes, overrideExisting: true);

        second.BlobName.ShouldNotBe(first.BlobName);
        (await ReadAsync(second)).ShouldBe(bytes);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, first.BlobName)).ShouldBeFalse();
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
                    "application/pdf", 1, new string('0', 64), nameIsAddress: true),
                autoSave: true)));

        await WithUnitOfWorkAsync(async () =>
            await _fileDescriptorManager.DeleteAsync(await _fileDescriptorRepository.GetAsync(first.Id)));

        var again = await CreateAsync(BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("again " + name));
        again.NameKey.ShouldBe(first.NameKey);
        (await FindAsync(BlobManagementTestContainers.Files, name))!.Id.ShouldBe(again.Id);
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
    /// When the name lock cannot be had, the save goes ahead on the unique name key alone: it is stored and found by
    /// its name, and its BLOB goes with it.
    /// </summary>
    [Fact]
    public async Task Without_The_Name_Lock_A_Save_Should_Go_Ahead()
    {
        var bytes = BlobManagementTestFiles.Pdf("lockless " + Guid.NewGuid());
        var name = "lockless-" + Guid.NewGuid().ToString("N") + ".pdf";

        var lockless = GetRequiredService<LocklessFileDescriptorManager>();
        var saved = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await lockless.CreateAsync(BlobManagementTestContainers.Files, name, stream);
        });

        saved.NameKey.ShouldNotBeNull();
        (await FindAsync(BlobManagementTestContainers.Files, name))!.Id.ShouldBe(saved.Id);
        (await ReadAsync(saved)).ShouldBe(bytes);

        await WithUnitOfWorkAsync(async () =>
            await _fileDescriptorManager.DeleteAsync(await _fileDescriptorRepository.GetAsync(saved.Id)));
        (await BlobExistsAsync(BlobManagementTestContainers.Files, saved.BlobName)).ShouldBeFalse();
    }

    /// <summary>
    /// The same bytes under two names are stored twice, a BLOB for each file, in one tenant as across tenants; deleting
    /// one file removes its BLOB and leaves the other's. The deleted row keeps its hash for the audit trail.
    /// </summary>
    [Fact]
    public async Task The_Same_Bytes_Should_Be_Stored_For_Each_File()
    {
        var bytes = BlobManagementTestFiles.Pdf("same " + Guid.NewGuid());

        var first = await CreateAsync(BlobManagementTestContainers.Files, "first-" + Guid.NewGuid().ToString("N") + ".pdf", bytes);
        var second = await CreateAsync(BlobManagementTestContainers.Files, "second-" + Guid.NewGuid().ToString("N") + ".pdf", bytes);

        second.BlobName.ShouldNotBe(first.BlobName);
        second.Hash.ShouldBe(first.Hash);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, first.BlobName)).ShouldBeTrue();
        (await ReadAsync(second)).ShouldBe(bytes);

        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(first));
        (await BlobExistsAsync(BlobManagementTestContainers.Files, first.BlobName)).ShouldBeFalse();
        (await ReadAsync(second)).ShouldBe(bytes);

        using (_dataFilter.Disable<ISoftDelete>())
        {
            var deleted = await WithUnitOfWorkAsync(() => _fileDescriptorRepository.GetAsync(first.Id));
            deleted.IsDeleted.ShouldBeTrue();
            deleted.Hash.ShouldBe(first.Hash);
        }

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            var elsewhere = await CreateAsync(BlobManagementTestContainers.Files, "first.pdf", bytes);
            elsewhere.BlobName.ShouldNotBe(second.BlobName);
        }
    }

    /// <summary>
    /// An insert that fails after the bytes were stored - here on the primary key, as the second use of an upload
    /// ticket does - removes its BLOB again.
    /// </summary>
    [Fact]
    public async Task A_Failed_Insert_Should_Leave_No_Blob()
    {
        var bytes = BlobManagementTestFiles.Pdf("taken id " + Guid.NewGuid());
        var taken = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _fileDescriptorManager.UploadAsync(BlobManagementTestContainers.Files, "taken.pdf", stream);
        });

        var recording = GetRequiredService<RecordingFileDescriptorManager>();
        await Should.ThrowAsync<Exception>(() => WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await recording.UploadAsync(BlobManagementTestContainers.Files, "again.pdf", stream, id: taken.Id);
        }));

        recording.GeneratedBlobNames.Count.ShouldBe(1);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, recording.GeneratedBlobNames[0])).ShouldBeFalse();
        (await ReadAsync(taken)).ShouldBe(bytes);
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
        }
    }
}
