using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The rules that keep a container's directories a tree, against the database provider of the concrete test class.
/// </summary>
public abstract class DirectoryManager_Tests<TStartupModule> : BlobManagementDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly DirectoryManager _directoryManager;
    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly IDirectoryDescriptorRepository _directoryRepository;
    private readonly IFileDescriptorRepository _fileRepository;

    protected DirectoryManager_Tests()
    {
        _directoryManager = GetRequiredService<DirectoryManager>();
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _directoryRepository = GetRequiredService<IDirectoryDescriptorRepository>();
        _fileRepository = GetRequiredService<IFileDescriptorRepository>();
    }

    [Fact]
    public async Task Directories_Should_Form_A_Tree_In_Their_Container()
    {
        var docs = await CreateAsync(BlobManagementTestContainers.Documents, "Docs", null);
        var year = await CreateAsync(BlobManagementTestContainers.Documents, "2026", docs.Id);
        var other = await CreateAsync(BlobManagementTestContainers.Documents, "Other", null);

        docs.ParentId.ShouldBeNull();
        docs.GetDepth().ShouldBe(1);
        year.Path.ShouldBe(docs.Id.ToString("N") + "/" + year.Id.ToString("N"));
        year.GetAncestorIds().ShouldBe(new[] { docs.Id });
        year.IsAtOrBelow(docs).ShouldBeTrue();
        other.Order.ShouldBeGreaterThan(docs.Order);

        await WithUnitOfWorkAsync(async () =>
        {
            (await _directoryRepository.GetChildrenAsync(BlobManagementTestContainers.Documents, null))
                .Select(d => d.Id).ShouldBe(new[] { docs.Id, other.Id });
            (await _directoryRepository.GetChildrenAsync(BlobManagementTestContainers.Documents, docs.Id))
                .Select(d => d.Id).ShouldBe(new[] { year.Id });
            (await _directoryRepository.GetIdsWithChildrenAsync(new[] { docs.Id, other.Id })).ShouldBe(new[] { docs.Id });
            (await _directoryRepository.HasChildrenAsync(other.Id)).ShouldBeFalse();
            (await _directoryManager.GetAncestorsAsync(await _directoryRepository.GetAsync(year.Id)))
                .Select(d => d.Id).ShouldBe(new[] { docs.Id });

            // The same names live in another container, apart from these.
            (await _directoryRepository.GetChildrenAsync(BlobManagementTestContainers.Archive, null)).ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task Sibling_Names_Should_Be_One_Name_Whatever_Their_Case()
    {
        var docs = await CreateAsync(BlobManagementTestContainers.Documents, "Docs", null);

        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryNameAlreadyExists,
            () => CreateAsync(BlobManagementTestContainers.Documents, "DOCS", null));

        // Under another parent, and in another container, the name is free.
        await CreateAsync(BlobManagementTestContainers.Documents, "docs", docs.Id);
        await CreateAsync(BlobManagementTestContainers.Archive, "Docs", null);

        var notes = await CreateAsync(BlobManagementTestContainers.Documents, "Notes", null);
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryNameAlreadyExists,
            () => RenameAsync(notes.Id, "docs"));

        // A change of case only is the directory's own name.
        (await RenameAsync(notes.Id, "NOTES")).Name.ShouldBe("NOTES");
        (await RenameAsync(notes.Id, "Minutes")).Name.ShouldBe("Minutes");
        await CreateAsync(BlobManagementTestContainers.Documents, "Notes", null);
    }

    [Fact]
    public async Task A_Move_Should_Keep_The_Tree_A_Tree()
    {
        var a = await CreateAsync(BlobManagementTestContainers.Documents, "A", null);
        var b = await CreateAsync(BlobManagementTestContainers.Documents, "B", a.Id);
        var c = await CreateAsync(BlobManagementTestContainers.Documents, "C", b.Id);
        var target = await CreateAsync(BlobManagementTestContainers.Documents, "Target", null);
        var elsewhere = await CreateAsync(BlobManagementTestContainers.Archive, "Elsewhere", null);

        // Not into itself, not below itself, not into another container.
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryCannotMoveIntoItself, () => MoveAsync(a.Id, a.Id));
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryCannotMoveIntoItself, () => MoveAsync(a.Id, c.Id));
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryNotFound, () => MoveAsync(a.Id, elsewhere.Id));

        // A name taken where it lands is refused.
        await CreateAsync(BlobManagementTestContainers.Documents, "b", target.Id);
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryNameAlreadyExists, () => MoveAsync(b.Id, target.Id));

        // The paths of everything below move along.
        await MoveAsync(a.Id, target.Id);
        await WithUnitOfWorkAsync(async () =>
        {
            var movedA = await _directoryRepository.GetAsync(a.Id);
            var movedC = await _directoryRepository.GetAsync(c.Id);
            movedA.ParentId.ShouldBe(target.Id);
            movedA.Path.ShouldBe(target.Id.ToString("N") + "/" + a.Id.ToString("N"));
            movedC.Path.ShouldBe(string.Join("/", new[] { target.Id, a.Id, b.Id, c.Id }.Select(id => id.ToString("N"))));
            movedC.GetDepth().ShouldBe(4);
        });

        // Back to the top, first among its siblings: the others move down.
        await MoveAsync(a.Id, null, order: 0);
        (await WithUnitOfWorkAsync(() => _directoryRepository.GetChildrenAsync(BlobManagementTestContainers.Documents, null)))
            .First().Id.ShouldBe(a.Id);
    }

    [Fact]
    public async Task A_Tree_Should_Be_At_Most_Sixteen_Levels_Deep()
    {
        Guid? parentId = null;
        DirectoryDescriptor last = null!;
        for (var level = 1; level <= DirectoryDescriptorConsts.MaxDepth; level++)
        {
            last = await CreateAsync(BlobManagementTestContainers.Documents, "level" + level, parentId);
            parentId = last.Id;
        }

        last.GetDepth().ShouldBe(DirectoryDescriptorConsts.MaxDepth);
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryTooDeep,
            () => CreateAsync(BlobManagementTestContainers.Documents, "one too many", last.Id));

        // A subtree is not moved to where its deepest directory would be too deep.
        var top = await CreateAsync(BlobManagementTestContainers.Documents, "Top", null);
        await CreateAsync(BlobManagementTestContainers.Documents, "Below", top.Id);
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryTooDeep, () => MoveAsync(top.Id, last.ParentId));
    }

    /// <summary>
    /// A deleted subdirectory is seen by nobody and restored by nothing: it does not make the directory it was in too
    /// deep to move.
    /// </summary>
    [Fact]
    public async Task A_Deleted_Subdirectory_Should_Not_Count_For_The_Depth_Of_A_Move()
    {
        Guid? parentId = null;
        DirectoryDescriptor deep = null!;
        for (var level = 1; level < DirectoryDescriptorConsts.MaxDepth; level++)
        {
            deep = await CreateAsync(BlobManagementTestContainers.Documents, "deep" + level, parentId);
            parentId = deep.Id;
        }

        var top = await CreateAsync(BlobManagementTestContainers.Documents, "Emptied", null);
        var below = await CreateAsync(BlobManagementTestContainers.Documents, "Below", top.Id);
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryTooDeep, () => MoveAsync(top.Id, deep.Id));

        await DeleteAsync(below.Id);
        await MoveAsync(top.Id, deep.Id);

        (await WithUnitOfWorkAsync(() => _directoryRepository.GetAsync(top.Id))).GetDepth().ShouldBe(DirectoryDescriptorConsts.MaxDepth);
    }

    [Fact]
    public async Task Only_An_Empty_Directory_Should_Be_Deleted()
    {
        var folder = await CreateAsync(BlobManagementTestContainers.Documents, "Folder", null);
        var child = await CreateAsync(BlobManagementTestContainers.Documents, "Child", folder.Id);

        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryNotEmpty, () => DeleteAsync(folder.Id));

        await DeleteAsync(child.Id);
        var file = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Pdf(Guid.NewGuid().ToString()));
            return await _fileDescriptorManager.UploadAsync(BlobManagementTestContainers.Documents, "kept.pdf", stream, folder.Id);
        });
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryNotEmpty, () => DeleteAsync(folder.Id));

        await WithUnitOfWorkAsync(async () => await _fileDescriptorManager.DeleteAsync(await _fileRepository.GetAsync(file.Id)));
        await DeleteAsync(folder.Id);

        (await WithUnitOfWorkAsync(() => _directoryRepository.FindAsync(folder.Id))).ShouldBeNull();
        // A deleted directory does not hold its name.
        await CreateAsync(BlobManagementTestContainers.Documents, "Folder", null);
    }

    [Fact]
    public async Task A_Container_Should_Have_Directories_Only_When_It_Allows_Them()
    {
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoriesNotAllowed,
            () => CreateAsync(BlobManagementTestContainers.Files, "Folder", null));
        await ShouldFailAsync(BlobManagementErrorCodes.ContainerNotFound,
            () => CreateAsync(BlobManagementTestContainers.NotManaged, "Folder", null));

        // A parent of another container is not found.
        var elsewhere = await CreateAsync(BlobManagementTestContainers.Archive, "Elsewhere", null);
        await ShouldFailAsync(BlobManagementErrorCodes.DirectoryNotFound,
            () => CreateAsync(BlobManagementTestContainers.Documents, "Folder", elsewhere.Id));
    }

    private Task<DirectoryDescriptor> CreateAsync(string containerName, string name, Guid? parentId)
    {
        return WithUnitOfWorkAsync(() => _directoryManager.CreateAsync(containerName, name, parentId));
    }

    private Task<DirectoryDescriptor> RenameAsync(Guid id, string name)
    {
        return WithUnitOfWorkAsync(async () => await _directoryManager.RenameAsync(await _directoryRepository.GetAsync(id), name));
    }

    private Task<DirectoryDescriptor> MoveAsync(Guid id, Guid? parentId, int order = 1)
    {
        return WithUnitOfWorkAsync(async () => await _directoryManager.MoveAsync(await _directoryRepository.GetAsync(id), parentId, order));
    }

    private Task DeleteAsync(Guid id)
    {
        return WithUnitOfWorkAsync(async () => await _directoryManager.DeleteAsync(await _directoryRepository.GetAsync(id)));
    }

    private static async Task ShouldFailAsync(string code, Func<Task> action)
    {
        var exception = await Should.ThrowAsync<BusinessException>(action);
        exception.Code.ShouldBe(code);
    }
}
