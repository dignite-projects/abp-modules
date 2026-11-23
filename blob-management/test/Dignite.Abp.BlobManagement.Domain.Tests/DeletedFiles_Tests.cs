using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// A file's life is its record's: a file kept by id is soft-deleted and keeps its bytes, comes back with
/// <see cref="FileDescriptorManager.RestoreAsync"/>, and is deleted for good - row and bytes - only when its business
/// system asks for it. A file saved under a name is deleted for good at once.
/// </summary>
public abstract class DeletedFiles_Tests<TStartupModule> : BlobManagementDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly FileDescriptorManager _files;
    private readonly DirectoryManager _directories;
    private readonly IFileDescriptorRepository _fileRepository;
    private readonly IDirectoryDescriptorRepository _directoryRepository;
    private readonly BlobManagementUsageManager _usage;
    private readonly IBlobContainerFactory _blobContainerFactory;
    private readonly IDataFilter _dataFilter;

    protected DeletedFiles_Tests()
    {
        _files = GetRequiredService<FileDescriptorManager>();
        _directories = GetRequiredService<DirectoryManager>();
        _fileRepository = GetRequiredService<IFileDescriptorRepository>();
        _directoryRepository = GetRequiredService<IDirectoryDescriptorRepository>();
        _usage = GetRequiredService<BlobManagementUsageManager>();
        _blobContainerFactory = GetRequiredService<IBlobContainerFactory>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    [Fact]
    public async Task A_File_Kept_By_Id_Should_Be_Soft_Deleted_And_Keep_Its_Bytes()
    {
        var file = await CreateFileAsync("kept-" + Guid.NewGuid().ToString("N") + ".pdf");

        await DeleteAsync(file.Id, permanently: false);

        // Gone from every read: by id, and from the listing of its place.
        (await WithUnitOfWorkAsync(() => _fileRepository.FindAsync(file.Id))).ShouldBeNull();
        (await WithUnitOfWorkAsync(() => _fileRepository.GetListAsync(BlobManagementTestContainers.Documents, null)))
            .ShouldNotContain(f => f.Id == file.Id);

        // But kept, with its bytes.
        var deleted = await FindIncludingDeletedAsync(file.Id);
        deleted.ShouldNotBeNull();
        deleted!.IsDeleted.ShouldBeTrue();
        deleted.DeletionTime.ShouldNotBeNull();
        (await BlobExistsAsync(file)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_Restored_File_Should_Be_As_It_Was()
    {
        var folder = await CreateDirectoryAsync("Restored-" + Guid.NewGuid().ToString("N"));
        var bytes = BlobManagementTestFiles.Pdf("back " + Guid.NewGuid());
        var file = await CreateFileAsync("back.pdf", folder.Id, bytes);

        await DeleteAsync(file.Id, permanently: false);
        var restored = await RestoreAsync(file.Id);

        restored.IsDeleted.ShouldBeFalse();
        restored.DeletionTime.ShouldBeNull();
        restored.DeleterId.ShouldBeNull();
        restored.DirectoryId.ShouldBe(folder.Id);
        restored.BlobName.ShouldBe(file.BlobName);

        var found = await WithUnitOfWorkAsync(() => _fileRepository.FindAsync(file.Id));
        found.ShouldNotBeNull();
        (await ReadAsync(found!)).ShouldBe(bytes);
    }

    /// <summary>
    /// A file that is soft-deleted does not keep its directory from being deleted; restored after it, it goes to the
    /// top of its container.
    /// </summary>
    [Fact]
    public async Task A_File_Whose_Directory_Was_Deleted_Should_Be_Restored_To_The_Top()
    {
        var folder = await CreateDirectoryAsync("Emptied-" + Guid.NewGuid().ToString("N"));
        var file = await CreateFileAsync("orphan.pdf", folder.Id);

        await DeleteAsync(file.Id, permanently: false);
        await WithUnitOfWorkAsync(async () =>
            await _directories.DeleteAsync(await _directoryRepository.GetAsync(folder.Id)));

        var restored = await RestoreAsync(file.Id);
        restored.IsDeleted.ShouldBeFalse();
        restored.DirectoryId.ShouldBeNull();
    }

    [Fact]
    public async Task A_File_Deleted_Permanently_Should_Lose_Its_Row_And_Its_Bytes()
    {
        // A live file, at once.
        var live = await CreateFileAsync("live.pdf");
        await DeleteAsync(live.Id, permanently: true);
        (await FindIncludingDeletedAsync(live.Id)).ShouldBeNull();
        (await BlobExistsAsync(live)).ShouldBeFalse();

        // A file soft-deleted before, as its business system deletes the record for good later.
        var later = await CreateFileAsync("later.pdf");
        await DeleteAsync(later.Id, permanently: false);
        (await BlobExistsAsync(later)).ShouldBeTrue();

        await DeleteAsync(later.Id, permanently: true);
        (await FindIncludingDeletedAsync(later.Id)).ShouldBeNull();
        (await BlobExistsAsync(later)).ShouldBeFalse();
    }

    /// <summary>
    /// ABP's <c>IBlobContainer</c> can neither restore a BLOB nor delete one for good, so a file saved under a name is
    /// deleted for good whatever is asked - and its name is free at once.
    /// </summary>
    [Fact]
    public async Task A_File_Saved_Under_A_Name_Should_Be_Deleted_For_Good()
    {
        var name = "named-" + Guid.NewGuid().ToString("N") + ".pdf";
        var saved = await SaveBlobAsync(name);

        await DeleteAsync(saved.Id, permanently: false);

        (await FindIncludingDeletedAsync(saved.Id)).ShouldBeNull();
        (await BlobExistsAsync(saved)).ShouldBeFalse();

        var again = await SaveBlobAsync(name);
        again.Id.ShouldNotBe(saved.Id);
    }

    /// <summary>
    /// What the allocation measures is what is stored: a soft-deleted file keeps its bytes, so it is counted until its
    /// business system deletes it for good.
    /// </summary>
    [Fact]
    public async Task Usage_Should_Count_A_Soft_Deleted_File_Until_It_Is_Deleted_For_Good()
    {
        var before = await WithUnitOfWorkAsync(() => _usage.GetUsedBytesAsync());
        var file = await CreateFileAsync("counted.pdf");
        var stored = await WithUnitOfWorkAsync(() => _usage.GetUsedBytesAsync());
        stored.ShouldBe(before + file.Size);

        await DeleteAsync(file.Id, permanently: false);
        (await WithUnitOfWorkAsync(() => _usage.GetUsedBytesAsync())).ShouldBe(stored);

        await DeleteAsync(file.Id, permanently: true);
        (await WithUnitOfWorkAsync(() => _usage.GetUsedBytesAsync())).ShouldBe(before);
    }

    private Task<FileDescriptor> CreateFileAsync(string name, Guid? directoryId = null, byte[]? bytes = null)
    {
        bytes ??= BlobManagementTestFiles.Pdf(name + " " + Guid.NewGuid());
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _files.CreateFileAsync(
                BlobManagementTestContainers.Documents, name, stream, new CreateFileArgs { DirectoryId = directoryId });
        });
    }

    private Task<FileDescriptor> SaveBlobAsync(string name)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Pdf(name + " " + Guid.NewGuid()));
            return await _files.SaveBlobAsync(BlobManagementTestContainers.Files, name, stream);
        });
    }

    private Task<DirectoryDescriptor> CreateDirectoryAsync(string name)
    {
        return WithUnitOfWorkAsync(() => _directories.CreateAsync(BlobManagementTestContainers.Documents, name, null));
    }

    private Task DeleteAsync(Guid id, bool permanently)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            FileDescriptor file;
            using (_dataFilter.Disable<ISoftDelete>())
            {
                file = await _fileRepository.GetAsync(id);
            }

            await _files.DeleteAsync(file, permanently);
        });
    }

    private Task<FileDescriptor> RestoreAsync(Guid id)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            FileDescriptor file;
            using (_dataFilter.Disable<ISoftDelete>())
            {
                file = await _fileRepository.GetAsync(id);
            }

            return await _files.RestoreAsync(file);
        });
    }

    private async Task<FileDescriptor?> FindIncludingDeletedAsync(Guid id)
    {
        using (_dataFilter.Disable<ISoftDelete>())
        {
            return await WithUnitOfWorkAsync(() => _fileRepository.FindAsync(id));
        }
    }

    private Task<bool> BlobExistsAsync(FileDescriptor file)
    {
        return _blobContainerFactory.Create(file.ContainerName).ExistsAsync(file.BlobName);
    }

    private Task<byte[]> ReadAsync(FileDescriptor file)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            await using var stream = await _files.GetStreamOrNullAsync(file);
            stream.ShouldNotBeNull();
            using var memory = new MemoryStream();
            await stream!.CopyToAsync(memory);
            return memory.ToArray();
        });
    }
}
