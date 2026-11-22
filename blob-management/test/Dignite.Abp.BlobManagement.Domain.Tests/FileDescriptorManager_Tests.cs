using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
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

    protected FileDescriptorManager_Tests()
    {
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _fileDescriptorRepository = GetRequiredService<IFileDescriptorRepository>();
        _blobContainerFactory = GetRequiredService<IBlobContainerFactory>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Should_Store_And_Read_Back_Under_The_Callers_Name_Tenant_Scoped()
    {
        var bytes = BlobManagementTestFiles.Pdf("tenant scoped");
        var tenantId = Guid.NewGuid();

        using (_currentTenant.Change(tenantId))
        {
            var descriptor = await SaveBlobAsync(BlobManagementTestContainers.Files, "2026/11/notes.pdf", bytes);

            descriptor.TenantId.ShouldBe(tenantId);
            descriptor.ContainerName.ShouldBe(BlobManagementTestContainers.Files);
            descriptor.Name.ShouldBe("2026/11/notes.pdf");
            Regex.IsMatch(descriptor.BlobName, "^[0-9a-f]{32}\\.pdf$").ShouldBeTrue();
            descriptor.MimeType.ShouldBe("application/pdf");
            descriptor.Size.ShouldBe(bytes.Length);
            // No checksum was sent to verify, so no hash was computed.
            descriptor.Hash.ShouldBeNull();

            var found = await FindBlobAsync(BlobManagementTestContainers.Files, "2026/11/notes.pdf");
            found.ShouldNotBeNull();
            found!.Id.ShouldBe(descriptor.Id);
            (await ReadAsync(found)).ShouldBe(bytes);
        }

        // Another tenant does not see the row, however it addresses it.
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            (await FindBlobAsync(BlobManagementTestContainers.Files, "2026/11/notes.pdf")).ShouldBeNull();
        }
    }

    [Fact]
    public async Task Should_Record_The_Writing_Client()
    {
        var descriptor = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Pdf("client " + Guid.NewGuid()));
            return await _fileDescriptorManager.SaveBlobAsync(
                BlobManagementTestContainers.Files, "client.pdf", stream, new SaveBlobArgs { ClientId = "site-app" });
        });

        descriptor.ClientId.ShouldBe("site-app");
    }

    [Fact]
    public async Task Should_Refuse_A_Container_The_BlobManagement_Did_Not_Declare()
    {
        var create = await Should.ThrowAsync<BusinessException>(() =>
            SaveBlobAsync(BlobManagementTestContainers.NotManaged, "notes.pdf", BlobManagementTestFiles.Pdf()));
        create.Code.ShouldBe(BlobManagementErrorCodes.ContainerNotFound);
        create.Data["ContainerName"].ShouldBe(BlobManagementTestContainers.NotManaged);

        var find = await Should.ThrowAsync<BusinessException>(() => FindBlobAsync(BlobManagementTestContainers.NotManaged, "notes.pdf"));
        find.Code.ShouldBe(BlobManagementErrorCodes.ContainerNotFound);
    }

    [Fact]
    public async Task Should_Refuse_An_Existing_Name_Unless_Overriding()
    {
        var name = "existing-" + Guid.NewGuid().ToString("N") + ".pdf";
        await SaveBlobAsync(BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("first " + name));

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            SaveBlobAsync(BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("second " + name)));
        exception.Code.ShouldBe(BlobManagementErrorCodes.BlobAlreadyExists);
    }

    /// <summary>
    /// An overriding save puts the new content in the file that has the name: the same file - its id, its name key -
    /// with the new bytes, still the one row under the name; the BLOB it had is gone once the save has committed.
    /// </summary>
    [Fact]
    public async Task Overriding_Should_Put_The_New_Content_In_The_Same_File_And_Remove_The_Old_Blob()
    {
        var name = "replaced-" + Guid.NewGuid().ToString("N") + ".pdf";
        var first = await SaveBlobAsync(BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("first " + name));
        var secondBytes = BlobManagementTestFiles.Pdf("second " + name);

        var second = await SaveBlobAsync(BlobManagementTestContainers.Files, name, secondBytes, overrideExisting: true);

        second.Id.ShouldBe(first.Id);
        second.NameKey.ShouldBe(first.NameKey);
        second.BlobName.ShouldNotBe(first.BlobName);
        (await FindBlobAsync(BlobManagementTestContainers.Files, name))!.Id.ShouldBe(first.Id);
        (await ReadAsync(second)).ShouldBe(secondBytes);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, first.BlobName)).ShouldBeFalse();
        await ShouldBeTheOnlyFileWithTheNameAsync(second);
    }

    [Fact]
    public async Task Overriding_With_The_Same_Bytes_Should_Store_Them_Anew()
    {
        var name = "same-" + Guid.NewGuid().ToString("N") + ".pdf";
        var bytes = BlobManagementTestFiles.Pdf(name);
        var first = await SaveBlobAsync(BlobManagementTestContainers.Files, name, bytes);

        var second = await SaveBlobAsync(BlobManagementTestContainers.Files, name, bytes, overrideExisting: true);

        second.Id.ShouldBe(first.Id);
        second.BlobName.ShouldNotBe(first.BlobName);
        (await ReadAsync(second)).ShouldBe(bytes);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, first.BlobName)).ShouldBeFalse();
        await ShouldBeTheOnlyFileWithTheNameAsync(second);
    }

    /// <summary>
    /// A read that found a file and then no bytes under its BLOB name was overtaken by an overriding save: the file is
    /// there, with another BLOB, and the read is given the descriptor it has now. A file that was not saved over since,
    /// or is gone, has none to give.
    /// </summary>
    [Fact]
    public async Task A_Read_Overtaken_By_An_Overriding_Save_Should_Find_What_The_File_Holds_Now()
    {
        var name = "overtaken-" + Guid.NewGuid().ToString("N") + ".pdf";
        var oldBytes = BlobManagementTestFiles.Pdf("old " + Guid.NewGuid());
        var newBytes = BlobManagementTestFiles.Pdf("new " + Guid.NewGuid());

        // What the read found, before the save.
        var found = await SaveBlobAsync(BlobManagementTestContainers.Files, name, oldBytes);
        (await WithUnitOfWorkAsync(() => _fileDescriptorManager.FindReplacedBlobAsync(found))).ShouldBeNull();

        await SaveBlobAsync(BlobManagementTestContainers.Files, name, newBytes, overrideExisting: true);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, found.BlobName)).ShouldBeFalse();

        var now = await WithUnitOfWorkAsync(() => _fileDescriptorManager.FindReplacedBlobAsync(found));
        now.ShouldNotBeNull();
        now!.Id.ShouldBe(found.Id);
        now.BlobName.ShouldNotBe(found.BlobName);
        (await ReadAsync(now)).ShouldBe(newBytes);

        await WithUnitOfWorkAsync(async () =>
            await _fileDescriptorManager.DeleteAsync(await _fileDescriptorRepository.GetAsync(found.Id)));
        (await WithUnitOfWorkAsync(() => _fileDescriptorManager.FindReplacedBlobAsync(found))).ShouldBeNull();
    }

    /// <summary>
    /// An overriding save counts the file for whoever saved it now, from now: its creation time is the save's, and its
    /// creator the user the save named.
    /// </summary>
    [Fact]
    public async Task Overriding_Should_Count_The_File_For_The_Save_That_Replaced_It()
    {
        var name = "recounted-" + Guid.NewGuid().ToString("N") + ".pdf";
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();

        var first = await SaveBlobAsync(
            BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("first " + name),
            new SaveBlobArgs { CreatorId = firstUser });
        first.CreatorId.ShouldBe(firstUser);
        // Read back, so both times have the precision the database keeps.
        var before = (await FindBlobAsync(BlobManagementTestContainers.Files, name))!;

        var second = await SaveBlobAsync(
            BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("second " + name),
            new SaveBlobArgs { OverrideExisting = true, CreatorId = secondUser });

        second.Id.ShouldBe(first.Id);
        var stored = (await FindBlobAsync(BlobManagementTestContainers.Files, name))!;
        stored.CreatorId.ShouldBe(secondUser);
        stored.CreationTime.ShouldBeGreaterThanOrEqualTo(before.CreationTime);
    }

    /// <summary>
    /// An overriding save whose unit of work fails after it stored the new bytes - here rolled back - deletes those
    /// bytes, and the BLOB the file had stays: the old one is deleted only once the save commits.
    /// <see cref="RollingBackFileDescriptorManager"/> stands for a transactional unit of work.
    /// </summary>
    [Fact]
    public async Task A_Rolled_Back_Override_Should_Keep_The_Old_Blob()
    {
        var name = "rolled-back-override-" + Guid.NewGuid().ToString("N") + ".pdf";
        var oldBytes = BlobManagementTestFiles.Pdf("old " + name);
        var first = await SaveBlobAsync(BlobManagementTestContainers.Files, name, oldBytes);

        var replaced = await SaveInUnitOfWorkAsync(
            (manager, stream) => manager.SaveBlobAsync(
                BlobManagementTestContainers.Files, name, stream, new SaveBlobArgs { OverrideExisting = true }),
            commit: false);

        replaced.BlobName.ShouldNotBe(first.BlobName);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, replaced.BlobName)).ShouldBeFalse();
        (await BlobExistsAsync(BlobManagementTestContainers.Files, first.BlobName)).ShouldBeTrue();
    }

    /// <summary>
    /// The lookup by name is check-then-act; the name key is the arbiter. A second descriptor under the same name -
    /// inserted past the manager's check, as the loser of two simultaneous first saves would be - is refused by the
    /// database, and the name is free again once its holder is deleted.
    /// </summary>
    [Fact]
    public async Task A_Name_Should_Have_One_Descriptor()
    {
        var name = "unique-" + Guid.NewGuid().ToString("N") + ".pdf";
        var first = await SaveBlobAsync(BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("first " + name));
        first.NameKey.ShouldBe(FileDescriptor.CreateNameKey(null, BlobManagementTestContainers.Files, name));

        await Should.ThrowAsync<Exception>(() => WithUnitOfWorkAsync(() =>
            _fileDescriptorRepository.InsertAsync(
                new FileDescriptor(
                    Guid.NewGuid(), BlobManagementTestContainers.Files, name, Guid.NewGuid().ToString("N") + ".pdf",
                    "application/pdf", 1, hash: null, nameIsAddress: true),
                autoSave: true)));

        await WithUnitOfWorkAsync(async () =>
            await _fileDescriptorManager.DeleteAsync(await _fileDescriptorRepository.GetAsync(first.Id)));

        var again = await SaveBlobAsync(BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("again " + name));
        again.NameKey.ShouldBe(first.NameKey);
        (await FindBlobAsync(BlobManagementTestContainers.Files, name))!.Id.ShouldBe(again.Id);
    }

    /// <summary>
    /// A name is an address as it was given, case and all, whatever the database's collation: two names that differ
    /// only in case are two files, each read back by its own name, and overriding one leaves the other as it was.
    /// </summary>
    [Fact]
    public async Task Names_Differing_Only_In_Case_Should_Be_Two_Files()
    {
        var stem = "case-" + Guid.NewGuid().ToString("N");
        var lower = stem + ".pdf";
        var upper = stem.ToUpperInvariant() + ".pdf";
        var lowerBytes = BlobManagementTestFiles.Pdf("lower " + stem);
        var upperBytes = BlobManagementTestFiles.Pdf("upper " + stem);

        var lowerFile = await SaveBlobAsync(BlobManagementTestContainers.Files, lower, lowerBytes);
        var upperFile = await SaveBlobAsync(BlobManagementTestContainers.Files, upper, upperBytes);

        upperFile.Id.ShouldNotBe(lowerFile.Id);
        upperFile.NameKey.ShouldNotBe(lowerFile.NameKey);
        (await FindBlobAsync(BlobManagementTestContainers.Files, lower))!.Id.ShouldBe(lowerFile.Id);
        (await FindBlobAsync(BlobManagementTestContainers.Files, upper))!.Id.ShouldBe(upperFile.Id);

        var newUpperBytes = BlobManagementTestFiles.Pdf("upper again " + stem);
        var replaced = await SaveBlobAsync(BlobManagementTestContainers.Files, upper, newUpperBytes, overrideExisting: true);

        replaced.Id.ShouldBe(upperFile.Id);
        (await FindBlobAsync(BlobManagementTestContainers.Files, upper))!.Id.ShouldBe(upperFile.Id);
        (await ReadAsync(replaced)).ShouldBe(newUpperBytes);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, upperFile.BlobName)).ShouldBeFalse();

        var untouched = await FindBlobAsync(BlobManagementTestContainers.Files, lower);
        untouched!.Id.ShouldBe(lowerFile.Id);
        (await ReadAsync(untouched)).ShouldBe(lowerBytes);
    }

    /// <summary>
    /// A save whose unit of work fails after the BLOB was stored takes the BLOB with it, when the failure rolls the row
    /// back. The test database runs without transactions, so the row itself is still there; with transactions it goes
    /// with the rollback, and its bytes are deleted with it. <see cref="RollingBackFileDescriptorManager"/> stands for a
    /// transactional unit of work.
    /// </summary>
    [Fact]
    public async Task A_Rolled_Back_Save_Should_Delete_Its_Blob()
    {
        var saved = await SaveInUnitOfWorkAsync(
            (manager, stream) => manager.SaveBlobAsync(
                BlobManagementTestContainers.Files, "rolled-back-save-" + Guid.NewGuid().ToString("N") + ".pdf", stream),
            commit: false);

        (await BlobExistsAsync(BlobManagementTestContainers.Files, saved.BlobName)).ShouldBeFalse();
    }

    /// <summary>As <see cref="A_Rolled_Back_Save_Should_Delete_Its_Blob"/>, for a file kept by its id.</summary>
    [Fact]
    public async Task A_Rolled_Back_File_Creation_Should_Delete_Its_Blob()
    {
        var created = await SaveInUnitOfWorkAsync(
            (manager, stream) => manager.CreateFileAsync(BlobManagementTestContainers.Files, "rolled-back-file.pdf", stream),
            commit: false);

        (await BlobExistsAsync(BlobManagementTestContainers.Files, created.BlobName)).ShouldBeFalse();
    }

    /// <summary>A save whose unit of work commits keeps its BLOB, even where a failure would have rolled it back.</summary>
    [Fact]
    public async Task A_Committed_Save_Should_Keep_Its_Blob()
    {
        var bytes = BlobManagementTestFiles.Pdf("committed " + Guid.NewGuid());
        var saved = await SaveInUnitOfWorkAsync(
            (manager, stream) => manager.SaveBlobAsync(
                BlobManagementTestContainers.Files, "committed-save-" + Guid.NewGuid().ToString("N") + ".pdf", stream),
            commit: true,
            bytes);

        (await BlobExistsAsync(BlobManagementTestContainers.Files, saved.BlobName)).ShouldBeTrue();
        (await ReadAsync(saved)).ShouldBe(bytes);
    }

    /// <summary>
    /// Stores through <see cref="RollingBackFileDescriptorManager"/> in a unit of work begun here, which is then
    /// completed or rolled back, and disposed.
    /// </summary>
    private async Task<FileDescriptor> SaveInUnitOfWorkAsync(
        Func<FileDescriptorManager, Stream, Task<FileDescriptor>> save,
        bool commit,
        byte[]? bytes = null)
    {
        using var scope = ServiceProvider.CreateScope();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var manager = scope.ServiceProvider.GetRequiredService<RollingBackFileDescriptorManager>();

        FileDescriptor saved;
        using (var unitOfWork = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions()))
        {
            using (var stream = new MemoryStream(bytes ?? BlobManagementTestFiles.Pdf("unit of work " + Guid.NewGuid())))
            {
                saved = await save(manager, stream);
            }

            (await BlobExistsAsync(BlobManagementTestContainers.Files, saved.BlobName)).ShouldBeTrue();

            if (commit)
            {
                await unitOfWork.CompleteAsync();
            }
            else
            {
                await unitOfWork.RollbackAsync();
            }
        }

        return saved;
    }

    /// <summary>
    /// The BLOB is deleted after commit: a unit of work that deletes the descriptor and then rolls back leaves the
    /// bytes in place. (The test database runs without transactions, so the row itself stays gone here; with
    /// transactions the rollback restores it, and its bytes are still there.)
    /// </summary>
    [Fact]
    public async Task A_Rolled_Back_Delete_Should_Keep_The_Blob()
    {
        var bytes = BlobManagementTestFiles.Pdf("rolled back " + Guid.NewGuid());
        var file = await SaveBlobAsync(BlobManagementTestContainers.Files, "rolled-back-" + Guid.NewGuid().ToString("N") + ".pdf", bytes);

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
    /// When the name lock cannot be had, the save goes ahead on the database's arbitration alone: it is stored and found by
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
            return await lockless.SaveBlobAsync(BlobManagementTestContainers.Files, name, stream);
        });

        saved.NameKey.ShouldNotBeNull();
        (await FindBlobAsync(BlobManagementTestContainers.Files, name))!.Id.ShouldBe(saved.Id);
        (await ReadAsync(saved)).ShouldBe(bytes);

        await WithUnitOfWorkAsync(async () =>
            await _fileDescriptorManager.DeleteAsync(await _fileDescriptorRepository.GetAsync(saved.Id)));
        (await BlobExistsAsync(BlobManagementTestContainers.Files, saved.BlobName)).ShouldBeFalse();
    }

    /// <summary>
    /// The same bytes under two names are stored twice, a BLOB for each file, in one tenant as across tenants; deleting
    /// one file removes its row and its BLOB and leaves the other's.
    /// </summary>
    [Fact]
    public async Task The_Same_Bytes_Should_Be_Stored_For_Each_File()
    {
        var bytes = BlobManagementTestFiles.Pdf("same " + Guid.NewGuid());

        var first = await SaveBlobAsync(BlobManagementTestContainers.Files, "first-" + Guid.NewGuid().ToString("N") + ".pdf", bytes);
        var second = await SaveBlobAsync(BlobManagementTestContainers.Files, "second-" + Guid.NewGuid().ToString("N") + ".pdf", bytes);

        second.BlobName.ShouldNotBe(first.BlobName);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, first.BlobName)).ShouldBeTrue();
        (await ReadAsync(second)).ShouldBe(bytes);

        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(first));
        (await BlobExistsAsync(BlobManagementTestContainers.Files, first.BlobName)).ShouldBeFalse();
        (await ReadAsync(second)).ShouldBe(bytes);

        (await WithUnitOfWorkAsync(() => _fileDescriptorRepository.FindAsync(first.Id))).ShouldBeNull();

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            var elsewhere = await SaveBlobAsync(BlobManagementTestContainers.Files, "first.pdf", bytes);
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
            return await _fileDescriptorManager.CreateFileAsync(BlobManagementTestContainers.Files, "taken.pdf", stream);
        });

        var recording = GetRequiredService<RecordingFileDescriptorManager>();
        await Should.ThrowAsync<Exception>(() => WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await recording.CreateFileAsync(
                BlobManagementTestContainers.Files, "again.pdf", stream, new CreateFileArgs { Id = taken.Id });
        }));

        recording.GeneratedBlobNames.Count.ShouldBe(1);
        (await BlobExistsAsync(BlobManagementTestContainers.Files, recording.GeneratedBlobNames[0])).ShouldBeFalse();
        (await ReadAsync(taken)).ShouldBe(bytes);
    }

    [Fact]
    public async Task Should_Refuse_Content_Its_Extension_Contradicts_And_Store_Nothing()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            SaveBlobAsync(BlobManagementTestContainers.Files, "picture.png", BlobManagementTestFiles.Pdf("not a picture")));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeMismatch);
        (await FindBlobAsync(BlobManagementTestContainers.Files, "picture.png")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Run_The_Containers_Contributors()
    {
        var tooLarge = await Should.ThrowAsync<BusinessException>(() =>
            SaveBlobAsync(BlobManagementTestContainers.Limited, "large.pdf", BlobManagementTestFiles.Pdf(new string('x', (int)BlobManagementTestContainers.LimitedMaxSizeInBytes))));
        tooLarge.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);

        var notAllowed = await Should.ThrowAsync<BusinessException>(() =>
            SaveBlobAsync(BlobManagementTestContainers.Limited, "notes.txt", BlobManagementTestFiles.Text("not a pdf")));
        notAllowed.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed);
        (await FindBlobAsync(BlobManagementTestContainers.Limited, "notes.txt")).ShouldBeNull();

        var allowed = await SaveBlobAsync(BlobManagementTestContainers.Limited, "small.pdf", BlobManagementTestFiles.Pdf("small"));
        allowed.MimeType.ShouldBe("application/pdf");
    }

    [Fact]
    public async Task A_Name_Without_An_Extension_Is_Typed_From_The_Content()
    {
        var descriptor = await SaveBlobAsync(BlobManagementTestContainers.Files, "no-extension-" + Guid.NewGuid().ToString("N"), BlobManagementTestFiles.Pdf("typed"));

        descriptor.MimeType.ShouldBe("application/pdf");
        descriptor.BlobName.ShouldEndWith(".pdf");
    }

    [Fact]
    public async Task A_File_Stored_Without_A_Checksum_Should_Have_No_Hash()
    {
        var descriptor = await SaveBlobAsync(BlobManagementTestContainers.Files, "unchecked-" + Guid.NewGuid().ToString("N") + ".pdf", BlobManagementTestFiles.Pdf("unchecked"));

        descriptor.Hash.ShouldBeNull();
        (await FindBlobAsync(BlobManagementTestContainers.Files, descriptor.Name))!.Hash.ShouldBeNull();
    }

    /// <summary>
    /// The checksum the caller sent is verified against the bytes that arrived - taken while they are copied in when the
    /// upload is not seekable, in a pass over them when it is - and kept as the file's hash, as it was sent.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Matching_Checksum_Should_Be_Verified_And_Kept(bool seekable)
    {
        var bytes = BlobManagementTestFiles.Pdf("verified " + Guid.NewGuid());
        var checksum = Sha256(bytes);
        var name = "verified-" + Guid.NewGuid().ToString("N") + ".pdf";

        var descriptor = await SaveWithChecksumAsync(BlobManagementTestContainers.Files, name, bytes, checksum, seekable);

        descriptor.Hash.ShouldBe(checksum);
        (await ReadAsync(descriptor)).ShouldBe(bytes);
        (await FindBlobAsync(BlobManagementTestContainers.Files, name))!.Hash.ShouldBe(checksum);
    }

    [Fact]
    public async Task A_Matching_Checksum_Should_Be_Verified_For_A_File_Kept_By_Id_Too()
    {
        var bytes = BlobManagementTestFiles.Pdf("by id " + Guid.NewGuid());
        var checksum = Sha256(bytes);

        var descriptor = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new NonSeekableStream(bytes);
            return await _fileDescriptorManager.CreateFileAsync(
                BlobManagementTestContainers.Files, "by-id.pdf", stream, new CreateFileArgs { ExpectedSha256 = checksum });
        });

        descriptor.Hash.ShouldBe(checksum);
        (await ReadAsync(descriptor)).ShouldBe(bytes);
    }

    /// <summary>A checksum the bytes do not have fails before anything is stored: no BLOB, no row.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Different_Checksum_Should_Fail_And_Store_Nothing(bool seekable)
    {
        var bytes = BlobManagementTestFiles.Pdf("tampered " + Guid.NewGuid());
        var other = Sha256(BlobManagementTestFiles.Pdf("what the caller hashed " + Guid.NewGuid()));
        var name = "tampered-" + Guid.NewGuid().ToString("N") + ".pdf";

        var recording = GetRequiredService<RecordingFileDescriptorManager>();
        var exception = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
        {
            using Stream stream = seekable ? new MemoryStream(bytes) : new NonSeekableStream(bytes);
            return await recording.SaveBlobAsync(
                BlobManagementTestContainers.Files, name, stream, new SaveBlobArgs { ExpectedSha256 = other });
        }));

        exception.Code.ShouldBe(BlobManagementErrorCodes.ChecksumMismatch);
        exception.Data["Expected"].ShouldBe(other);
        exception.Data["Actual"].ShouldBe(Sha256(bytes));
        recording.GeneratedBlobNames.ShouldBeEmpty();
        (await FindBlobAsync(BlobManagementTestContainers.Files, name)).ShouldBeNull();
    }

    /// <summary>
    /// A value that is not base64 of 32 bytes cannot be checked, and is refused without reading a byte of the upload.
    /// </summary>
    [Theory]
    [InlineData("this is not base64!")]
    [InlineData("AAAA")]
    public async Task A_Checksum_That_Is_Not_A_Sha256_Should_Be_Refused_Before_The_Upload_Is_Read(string checksum)
    {
        using var stream = new NonSeekableStream(BlobManagementTestFiles.Pdf("never read"));

        var exception = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            _fileDescriptorManager.SaveBlobAsync(
                BlobManagementTestContainers.Files, "invalid-" + Guid.NewGuid().ToString("N") + ".pdf", stream,
                new SaveBlobArgs { ExpectedSha256 = checksum })));

        exception.Code.ShouldBe(BlobManagementErrorCodes.ChecksumInvalid);
        stream.BytesRead.ShouldBe(0);
    }

    /// <summary>
    /// An overriding save whose new bytes fail their checksum stores nothing and leaves the file as it was: its id, its
    /// BLOB and its bytes.
    /// </summary>
    [Fact]
    public async Task A_Failed_Check_On_An_Overriding_Save_Should_Leave_The_File_As_It_Was()
    {
        var name = "override-" + Guid.NewGuid().ToString("N") + ".pdf";
        var oldBytes = BlobManagementTestFiles.Pdf("old " + Guid.NewGuid());
        var first = await SaveBlobAsync(BlobManagementTestContainers.Files, name, oldBytes);

        var exception = await Should.ThrowAsync<BusinessException>(() => SaveWithChecksumAsync(
            BlobManagementTestContainers.Files, name, BlobManagementTestFiles.Pdf("new " + Guid.NewGuid()),
            Sha256(BlobManagementTestFiles.Pdf("elsewhere")), seekable: true, overrideExisting: true));
        exception.Code.ShouldBe(BlobManagementErrorCodes.ChecksumMismatch);

        await ShouldBeAsItWasAsync(BlobManagementTestContainers.Files, name, first, oldBytes);
    }

    /// <summary>
    /// An overriding save whose new bytes the container's rules refuse stores nothing and leaves the file as it was.
    /// </summary>
    [Fact]
    public async Task A_Refused_Overriding_Save_Should_Leave_The_File_As_It_Was()
    {
        var name = "refused-" + Guid.NewGuid().ToString("N") + ".pdf";
        var oldBytes = BlobManagementTestFiles.Pdf("small " + name);
        var first = await SaveBlobAsync(BlobManagementTestContainers.Limited, name, oldBytes);

        var tooLarge = BlobManagementTestFiles.Pdf(new string('x', (int)BlobManagementTestContainers.LimitedMaxSizeInBytes));
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            SaveBlobAsync(BlobManagementTestContainers.Limited, name, tooLarge, overrideExisting: true));
        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTooLarge);

        await ShouldBeAsItWasAsync(BlobManagementTestContainers.Limited, name, first, oldBytes);
    }

    private async Task ShouldBeAsItWasAsync(string containerName, string name, FileDescriptor before, byte[] bytes)
    {
        var found = await FindBlobAsync(containerName, name);
        found.ShouldNotBeNull();
        found!.Id.ShouldBe(before.Id);
        found.BlobName.ShouldBe(before.BlobName);
        found.Size.ShouldBe(before.Size);
        (await ReadAsync(found)).ShouldBe(bytes);
    }

    private static string Sha256(byte[] bytes) => Convert.ToBase64String(SHA256.HashData(bytes));

    private Task<FileDescriptor> SaveWithChecksumAsync(
        string containerName, string name, byte[] bytes, string checksum, bool seekable, bool overrideExisting = false)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using Stream stream = seekable ? new MemoryStream(bytes) : new NonSeekableStream(bytes);
            return await _fileDescriptorManager.SaveBlobAsync(
                containerName, name, stream,
                new SaveBlobArgs { OverrideExisting = overrideExisting, ExpectedSha256 = checksum });
        });
    }

    /// <summary>A request body: forward-only, as ASP.NET Core's is. Counts what is read from it.</summary>
    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableStream(byte[] bytes)
        {
            _inner = new MemoryStream(bytes);
        }

        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private Task<FileDescriptor> SaveBlobAsync(string containerName, string name, byte[] bytes, bool overrideExisting = false)
    {
        return SaveBlobAsync(containerName, name, bytes, new SaveBlobArgs { OverrideExisting = overrideExisting });
    }

    private Task<FileDescriptor> SaveBlobAsync(string containerName, string name, byte[] bytes, SaveBlobArgs args)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _fileDescriptorManager.SaveBlobAsync(containerName, name, stream, args);
        });
    }

    private Task<FileDescriptor?> FindBlobAsync(string containerName, string name)
    {
        return WithUnitOfWorkAsync(() => _fileDescriptorManager.FindBlobAsync(containerName, name));
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

    /// <summary><paramref name="holder"/> is the one row with its name in its container, and holds the name key.</summary>
    private async Task ShouldBeTheOnlyFileWithTheNameAsync(FileDescriptor holder)
    {
        var rows = (await WithUnitOfWorkAsync(() => _fileDescriptorRepository.GetListAsync()))
            .Where(f => f.ContainerName == holder.ContainerName && f.Name == holder.Name)
            .ToList();
        rows.Count.ShouldBe(1);
        rows[0].Id.ShouldBe(holder.Id);
        rows[0].NameKey.ShouldBe(FileDescriptor.CreateNameKey(holder.TenantId, holder.ContainerName, holder.Name));
    }
}
