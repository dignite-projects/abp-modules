using System;
using System.Threading.Tasks;
using Dignite.FileExplorer.Files;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;

namespace Dignite.FileExplorer.Directories;

public class DirectoryManager_Integration_Tests : FileExplorerDomainTestBase
{
    private readonly DirectoryManager _directoryManager;
    private readonly IDirectoryDescriptorRepository _directoryRepository;
    private readonly IFileDescriptorRepository _fileRepository;
    private readonly IDataFilter<ISoftDelete> _softDeleteFilter;

    public DirectoryManager_Integration_Tests()
    {
        _directoryManager = GetRequiredService<DirectoryManager>();
        _directoryRepository = GetRequiredService<IDirectoryDescriptorRepository>();
        _fileRepository = GetRequiredService<IFileDescriptorRepository>();
        _softDeleteFilter = GetRequiredService<IDataFilter<ISoftDelete>>();
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteDirectory_WhenOnlySoftDeletedFilesRemain()
    {
        var directory = new DirectoryDescriptor(
            Guid.NewGuid(),
            "Default",
            "soft-deleted-files-only",
            null,
            0,
            null)
        {
            CreatorId = Guid.NewGuid()
        };
        var file = new FileDescriptor(
            Guid.NewGuid(),
            directory.ContainerName,
            Guid.NewGuid().ToString("N"),
            "deleted.txt",
            "text/plain",
            string.Empty,
            directory.Id,
            string.Empty,
            null);

        await WithUnitOfWorkAsync(async () =>
        {
            await _directoryRepository.InsertAsync(directory, autoSave: true);
            await _fileRepository.InsertAsync(file, autoSave: true);
            await _fileRepository.DeleteAsync(file, autoSave: true);
        });

        await WithUnitOfWorkAsync(() => _directoryManager.DeleteAsync(directory));

        await WithUnitOfWorkAsync(async () =>
        {
            (await _directoryRepository.FindAsync(directory.Id)).ShouldBeNull();

            using (_softDeleteFilter.Disable())
            {
                var deletedFile = await _fileRepository.GetAsync(file.Id);
                deletedFile.IsDeleted.ShouldBeTrue();
                deletedFile.DirectoryId.ShouldBeNull();
            }
        });
    }
}
