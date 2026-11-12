using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The files people upload through <see cref="FileDescriptorManager"/> - addressed by their ids, with names that
/// repeat, filed in directories, renamed and moved - beside the files applications save under a name, in the same
/// containers.
/// </summary>
public abstract class UploadedFiles_Tests<TStartupModule> : BlobManagementDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly DirectoryManager _directoryManager;
    private readonly IFileDescriptorRepository _fileRepository;

    protected UploadedFiles_Tests()
    {
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _directoryManager = GetRequiredService<DirectoryManager>();
        _fileRepository = GetRequiredService<IFileDescriptorRepository>();
    }

    [Fact]
    public async Task Names_Of_Uploaded_Files_Should_Repeat()
    {
        var first = await UploadAsync(BlobManagementTestContainers.Documents, "minutes.pdf", "first");
        var second = await UploadAsync(BlobManagementTestContainers.Documents, "minutes.pdf", "second");

        second.Id.ShouldNotBe(first.Id);
        // An uploaded file's name is not an address: no name key, so the unique index does not keep them apart.
        first.NameKey.ShouldBeNull();
        second.NameKey.ShouldBeNull();
        first.IsAddressedByName.ShouldBeFalse();
        first.MimeType.ShouldBe("application/pdf");
    }

    [Fact]
    public async Task Same_Content_Should_Share_One_Blob()
    {
        var content = "shared content " + Guid.NewGuid();

        var first = await UploadAsync(BlobManagementTestContainers.Documents, "a.pdf", content);
        var second = await UploadAsync(BlobManagementTestContainers.Documents, "b.pdf", content);

        second.BlobName.ShouldBe(first.BlobName);
        second.OwnsBlob().ShouldBeFalse();
    }

    [Fact]
    public async Task A_Callback_Should_Set_Fields_Before_The_Row_Is_Inserted_When_Storing_And_When_Sharing()
    {
        var content = "configured " + Guid.NewGuid();

        var stored = await UploadAsync(BlobManagementTestContainers.Documents, "first.pdf", content, configureFile: file => file.SetProperty("Test:Marker", "stored"));
        var shared = await UploadAsync(BlobManagementTestContainers.Documents, "second.pdf", content, configureFile: file => file.SetProperty("Test:Marker", "shared"));

        stored.OwnsBlob().ShouldBeTrue();
        shared.OwnsBlob().ShouldBeFalse();
        shared.BlobName.ShouldBe(stored.BlobName);

        // Read back: the values were in the inserted rows, which nothing updated.
        (await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(stored.Id))).GetProperty<string>("Test:Marker").ShouldBe("stored");
        (await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(shared.Id))).GetProperty<string>("Test:Marker").ShouldBe("shared");
        stored.LastModificationTime.ShouldBeNull();
        shared.LastModificationTime.ShouldBeNull();
    }

    [Fact]
    public async Task A_Failing_Callback_Should_Fail_The_Upload_And_Store_Nothing()
    {
        var name = Guid.NewGuid().ToString("N") + ".pdf";

        await Should.ThrowAsync<InvalidOperationException>(() => UploadAsync(
            BlobManagementTestContainers.Documents, name, configureFile: _ => throw new InvalidOperationException("refused")));

        (await WithUnitOfWorkAsync(() => _fileRepository.GetListAsync())).ShouldNotContain(f => f.Name == name);
    }

    [Fact]
    public async Task The_Two_Ways_In_Should_Meet_In_One_Container()
    {
        // An application saves a BLOB under a name and reads it back by it...
        var saved = await SaveAsync(BlobManagementTestContainers.Documents, "report.pdf");
        saved.IsAddressedByName.ShouldBeTrue();

        // ...and a person uploads a file of the same name: another file, which is no address.
        var uploaded = await UploadAsync(BlobManagementTestContainers.Documents, "report.pdf");
        uploaded.IsAddressedByName.ShouldBeFalse();

        (await WithUnitOfWorkAsync(() => _fileDescriptorManager.FindAsync(BlobManagementTestContainers.Documents, "report.pdf")))!
            .Id.ShouldBe(saved.Id);

        // The top of the container lists both.
        var listed = await WithUnitOfWorkAsync(() => _fileRepository.GetListAsync(BlobManagementTestContainers.Documents, null));
        listed.Select(f => f.Id).ShouldBe(new[] { saved.Id, uploaded.Id }, ignoreOrder: true);

        // Uploading works in a container without directories too, and saving in one with them.
        (await UploadAsync(BlobManagementTestContainers.Files, "a.pdf")).IsAddressedByName.ShouldBeFalse();
    }

    [Fact]
    public async Task A_File_Addressed_By_Its_Name_Should_Not_Be_Renamed_Or_Moved()
    {
        var saved = await SaveAsync(BlobManagementTestContainers.Documents, "themes/site.css.pdf");
        var folder = await CreateDirectoryAsync(BlobManagementTestContainers.Documents, "Folder", null);

        await ShouldFailAsync(BlobManagementErrorCodes.FileAddressedByName, () => WithUnitOfWorkAsync(async () =>
            await _fileDescriptorManager.RenameAsync(await _fileRepository.GetAsync(saved.Id), "other.pdf")));
        await ShouldFailAsync(BlobManagementErrorCodes.FileAddressedByName, () => WithUnitOfWorkAsync(async () =>
            await _fileDescriptorManager.MoveAsync(await _fileRepository.GetAsync(saved.Id), folder.Id)));

        var unchanged = await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(saved.Id));
        unchanged.Name.ShouldBe("themes/site.css.pdf");
        unchanged.DirectoryId.ShouldBeNull();
    }

    [Fact]
    public async Task An_Uploaded_File_Should_Be_Filed_Renamed_And_Moved()
    {
        var first = await CreateDirectoryAsync(BlobManagementTestContainers.Documents, "First", null);
        var second = await CreateDirectoryAsync(BlobManagementTestContainers.Documents, "Second", first.Id);
        var elsewhere = await CreateDirectoryAsync(BlobManagementTestContainers.Archive, "Elsewhere", null);

        // Filed before it is inserted: the row is written once.
        var file = await UploadAsync(BlobManagementTestContainers.Documents, "draft.pdf", directoryId: first.Id);
        file.DirectoryId.ShouldBe(first.Id);
        file.LastModificationTime.ShouldBeNull();

        await WithUnitOfWorkAsync(async () =>
        {
            var loaded = await _fileRepository.GetAsync(file.Id);
            await _fileDescriptorManager.MoveAsync(loaded, second.Id);
            await _fileDescriptorManager.RenameAsync(loaded, "final.pdf");
        });

        var moved = await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(file.Id));
        moved.DirectoryId.ShouldBe(second.Id);
        moved.Name.ShouldBe("final.pdf");
        moved.BlobName.ShouldBe(file.BlobName);

        // Back to the top of its container.
        await WithUnitOfWorkAsync(async () => await _fileDescriptorManager.MoveAsync(await _fileRepository.GetAsync(file.Id), null));
        (await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(file.Id))).DirectoryId.ShouldBeNull();

        // A file never changes containers, and a container without directories files nothing.
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryNotFound, () => WithUnitOfWorkAsync(async () =>
            await _fileDescriptorManager.MoveAsync(await _fileRepository.GetAsync(file.Id), elsewhere.Id)));
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryNotFound,
            () => UploadAsync(BlobManagementTestContainers.Documents, "a.pdf", directoryId: elsewhere.Id));
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoriesNotAllowed,
            () => UploadAsync(BlobManagementTestContainers.Files, "a.pdf", directoryId: first.Id));
    }

    [Fact]
    public async Task A_Place_Should_List_Its_Files_Searched_Sorted_And_Paged()
    {
        var folder = await CreateDirectoryAsync(BlobManagementTestContainers.Documents, "Listing", null);
        var report = await UploadAsync(BlobManagementTestContainers.Documents, "Report-2026.pdf", directoryId: folder.Id);
        var notes = await UploadAsync(BlobManagementTestContainers.Documents, "notes.pdf", directoryId: folder.Id);
        var same = new[]
        {
            await UploadAsync(BlobManagementTestContainers.Documents, "scan.pdf", directoryId: folder.Id),
            await UploadAsync(BlobManagementTestContainers.Documents, "scan.pdf", directoryId: folder.Id),
            await UploadAsync(BlobManagementTestContainers.Documents, "scan.pdf", directoryId: folder.Id)
        };
        var atTop = await UploadAsync(BlobManagementTestContainers.Documents, "top.pdf");
        var saved = await SaveAsync(BlobManagementTestContainers.Documents, "saved.pdf");

        await WithUnitOfWorkAsync(async () =>
        {
            (await _fileRepository.GetCountAsync(BlobManagementTestContainers.Documents, folder.Id)).ShouldBe(5);
            (await _fileRepository.AnyInDirectoryAsync(folder.Id)).ShouldBeTrue();
            (await _fileRepository.AnyInDirectoryAsync(Guid.NewGuid())).ShouldBeFalse();

            // A search finds a name whatever its case.
            (await _fileRepository.GetListAsync(BlobManagementTestContainers.Documents, folder.Id, filter: "report"))
                .Select(f => f.Id).ShouldBe(new[] { report.Id });
            (await _fileRepository.GetCountAsync(BlobManagementTestContainers.Documents, folder.Id, filter: "SCAN")).ShouldBe(3);

            var byName = await _fileRepository.GetListAsync(BlobManagementTestContainers.Documents, folder.Id, sorting: "name desc");
            // Where "Report" sorts against "notes" is the database's collation; "scan" is last in every one.
            byName.First().Name.ShouldBe("scan.pdf");
            byName.Select(f => f.Id).ShouldContain(notes.Id);
            byName.Last().Name.ShouldNotBe("scan.pdf");

            // Names repeat, so the id ends every order: the pages of one listing neither share a file nor miss one.
            var paged = new System.Collections.Generic.List<Guid>();
            for (var skip = 0; skip < 5; skip += 2)
            {
                paged.AddRange((await _fileRepository.GetListAsync(
                    BlobManagementTestContainers.Documents, folder.Id, sorting: "Name", maxResultCount: 2, skipCount: skip)).Select(f => f.Id));
            }

            paged.Distinct().Count().ShouldBe(5);
            paged.ShouldContain(same[0].Id);
            paged.ShouldContain(same[2].Id);

            // The top of the container: what is filed nowhere, the files applications saved among them.
            (await _fileRepository.GetListAsync(BlobManagementTestContainers.Documents, null))
                .Select(f => f.Id).ShouldBe(new[] { atTop.Id, saved.Id }, ignoreOrder: true);
            // The unfiled uploads only - what an import of directories reads.
            (await _fileRepository.GetUnfiledListAsync(BlobManagementTestContainers.Documents, 0, 10))
                .Select(f => f.Id).ShouldBe(new[] { atTop.Id });
        });

        var unsupported = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            _fileRepository.GetListAsync(BlobManagementTestContainers.Documents, folder.Id, sorting: "Hash")));
        unsupported.Code.ShouldBe(BlobManagementErrorCodes.SortingNotSupported);
    }

    private Task<FileDescriptor> UploadAsync(
        string containerName,
        string name,
        string? content = null,
        Guid? directoryId = null,
        Action<FileDescriptor>? configureFile = null)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Pdf(content ?? Guid.NewGuid().ToString()));
            return await _fileDescriptorManager.UploadAsync(containerName, name, stream, directoryId, configureFile);
        });
    }

    private Task<FileDescriptor> SaveAsync(string containerName, string name)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString()));
            return await _fileDescriptorManager.CreateAsync(containerName, name, stream);
        });
    }

    private Task<DirectoryDescriptor> CreateDirectoryAsync(string containerName, string name, Guid? parentId)
    {
        return WithUnitOfWorkAsync(() => _directoryManager.CreateAsync(containerName, name, parentId));
    }

    private static async Task ShouldFailAsync(string code, Func<Task> action)
    {
        var exception = await Should.ThrowAsync<BusinessException>(action);
        exception.Code.ShouldBe(code);
    }
}
