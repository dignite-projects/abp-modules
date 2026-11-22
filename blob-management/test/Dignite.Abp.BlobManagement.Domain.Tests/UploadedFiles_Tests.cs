using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;
using Volo.Abp.Data;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The files an application keeps by id through <see cref="FileDescriptorManager"/> - with names that repeat, filed in
/// directories, renamed and moved - beside the BLOBs applications save under a name, in the same containers.
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
    public async Task Names_Of_Files_Kept_By_Id_Should_Repeat()
    {
        var first = await CreateFileAsync(BlobManagementTestContainers.Documents, "minutes.pdf", "first");
        var second = await CreateFileAsync(BlobManagementTestContainers.Documents, "minutes.pdf", "second");

        second.Id.ShouldNotBe(first.Id);
        // The name of a file kept by id is not an address: no name key, so the unique index does not keep them apart.
        first.NameKey.ShouldBeNull();
        second.NameKey.ShouldBeNull();
        first.IsAddressedByName.ShouldBeFalse();
        first.MimeType.ShouldBe("application/pdf");
    }

    [Fact]
    public async Task Same_Content_Should_Be_Stored_For_Each_File()
    {
        var content = "same content " + Guid.NewGuid();

        var first = await CreateFileAsync(BlobManagementTestContainers.Documents, "a.pdf", content);
        var second = await CreateFileAsync(BlobManagementTestContainers.Documents, "b.pdf", content);

        second.BlobName.ShouldNotBe(first.BlobName);
    }

    /// <summary>
    /// What the application says with the file - its id, its directory, whether it is public or pending, whom it is
    /// counted for, the client - is in the row as it was inserted, which nothing updated.
    /// </summary>
    [Fact]
    public async Task What_The_Application_Says_Should_Be_In_The_Inserted_Row()
    {
        var folder = await CreateDirectoryAsync(BlobManagementTestContainers.Documents, "Said", null);
        var args = new CreateFileArgs
        {
            Id = Guid.NewGuid(),
            DirectoryId = folder.Id,
            IsPublic = true,
            IsPending = true,
            CreatorId = Guid.NewGuid(),
            ClientId = "site-app"
        };
        args.ExtraProperties["LegacyPath"] = "Docs/2026";

        var stored = await CreateFileAsync(BlobManagementTestContainers.Documents, "said.pdf", args: args);

        var read = await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(stored.Id));
        read.Id.ShouldBe(args.Id.Value);
        read.DirectoryId.ShouldBe(folder.Id);
        read.IsPublic.ShouldBeTrue();
        read.IsPending.ShouldBeTrue();
        read.CreatorId.ShouldBe(args.CreatorId);
        read.ClientId.ShouldBe("site-app");
        read.GetProperty<string>("LegacyPath").ShouldBe("Docs/2026");
        read.LastModificationTime.ShouldBeNull();
    }

    [Fact]
    public async Task The_Two_Ways_In_Should_Meet_In_One_Container()
    {
        // An application saves a BLOB under a name and reads it back by it...
        var saved = await SaveAsync(BlobManagementTestContainers.Documents, "report.pdf");
        saved.IsAddressedByName.ShouldBeTrue();

        // ...and keeps a file of the same name by id: another file, which is no address.
        var kept = await CreateFileAsync(BlobManagementTestContainers.Documents, "report.pdf");
        kept.IsAddressedByName.ShouldBeFalse();

        (await WithUnitOfWorkAsync(() => _fileDescriptorManager.FindBlobAsync(BlobManagementTestContainers.Documents, "report.pdf")))!
            .Id.ShouldBe(saved.Id);

        // The top of the container lists both.
        var listed = await WithUnitOfWorkAsync(() => _fileRepository.GetListAsync(BlobManagementTestContainers.Documents, null));
        listed.Select(f => f.Id).ShouldBe(new[] { saved.Id, kept.Id }, ignoreOrder: true);

        // Keeping by id works in a container without directories too, and saving in one with them.
        (await CreateFileAsync(BlobManagementTestContainers.Files, "a.pdf")).IsAddressedByName.ShouldBeFalse();
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
    public async Task A_File_Kept_By_Id_Should_Be_Filed_Renamed_And_Moved()
    {
        var first = await CreateDirectoryAsync(BlobManagementTestContainers.Documents, "First", null);
        var second = await CreateDirectoryAsync(BlobManagementTestContainers.Documents, "Second", first.Id);
        var elsewhere = await CreateDirectoryAsync(BlobManagementTestContainers.Archive, "Elsewhere", null);

        // Filed before it is inserted: the row is written once.
        var file = await CreateFileAsync(BlobManagementTestContainers.Documents, "draft.pdf", directoryId: first.Id);
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
            () => CreateFileAsync(BlobManagementTestContainers.Documents, "a.pdf", directoryId: elsewhere.Id));
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoriesNotAllowed,
            () => CreateFileAsync(BlobManagementTestContainers.Files, "a.pdf", directoryId: first.Id));
    }

    [Fact]
    public async Task A_Place_Should_List_Its_Files_Searched_Sorted_And_Paged()
    {
        var folder = await CreateDirectoryAsync(BlobManagementTestContainers.Documents, "Listing", null);
        var report = await CreateFileAsync(BlobManagementTestContainers.Documents, "Report-2026.pdf", directoryId: folder.Id);
        var notes = await CreateFileAsync(BlobManagementTestContainers.Documents, "notes.pdf", directoryId: folder.Id);
        var same = new[]
        {
            await CreateFileAsync(BlobManagementTestContainers.Documents, "scan.pdf", directoryId: folder.Id),
            await CreateFileAsync(BlobManagementTestContainers.Documents, "scan.pdf", directoryId: folder.Id),
            await CreateFileAsync(BlobManagementTestContainers.Documents, "scan.pdf", directoryId: folder.Id)
        };
        var atTop = await CreateFileAsync(BlobManagementTestContainers.Documents, "top.pdf");
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
            // The unfiled files kept by id only - what an import of directories reads.
            (await _fileRepository.GetUnfiledListAsync(BlobManagementTestContainers.Documents, 0, 10))
                .Select(f => f.Id).ShouldBe(new[] { atTop.Id });
        });

        var unsupported = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            _fileRepository.GetListAsync(BlobManagementTestContainers.Documents, folder.Id, sorting: "Hash")));
        unsupported.Code.ShouldBe(BlobManagementErrorCodes.SortingNotSupported);
    }

    private Task<FileDescriptor> CreateFileAsync(
        string containerName,
        string name,
        string? content = null,
        Guid? directoryId = null,
        CreateFileArgs? args = null)
    {
        args ??= new CreateFileArgs { DirectoryId = directoryId };
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Pdf(content ?? Guid.NewGuid().ToString()));
            return await _fileDescriptorManager.CreateFileAsync(containerName, name, stream, args);
        });
    }

    private Task<FileDescriptor> SaveAsync(string containerName, string name)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString()));
            return await _fileDescriptorManager.SaveBlobAsync(containerName, name, stream);
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
