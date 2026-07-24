using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Dignite.FileExplorer.Directories;
using Dignite.FileExplorer.Files;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.FileExplorer.DirectorySafety.Tests.Directories;

public class DirectoryManager_Tests
{
    [Fact]
    public async Task MoveAsync_ShouldRejectMovingDirectoryIntoItself()
    {
        var directory = CreateDirectory(Guid.NewGuid(), null);
        var repository = Substitute.For<IDirectoryDescriptorRepository>();
        repository.GetAsync(directory.Id).Returns(directory);

        var manager = new DirectoryManager(repository, new ContainerNameValidator());

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            manager.MoveAsync(directory, directory.Id, 0));

        exception.Code.ShouldBe(FileExplorerErrorCodes.Directories.ForbidMovingToChild);
    }

    [Fact]
    public async Task MoveAsync_ShouldRejectMovingDirectoryIntoDescendant()
    {
        var root = CreateDirectory(Guid.NewGuid(), null);
        var child = CreateDirectory(Guid.NewGuid(), root.Id);
        var repository = Substitute.For<IDirectoryDescriptorRepository>();
        repository.GetAsync(child.Id).Returns(child);
        repository.GetAsync(root.Id).Returns(root);

        var manager = new DirectoryManager(repository, new ContainerNameValidator());

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            manager.MoveAsync(root, child.Id, 0));

        exception.Code.ShouldBe(FileExplorerErrorCodes.Directories.ForbidMovingToChild);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectMissingParent()
    {
        var parentId = Guid.NewGuid();
        var repository = Substitute.For<IDirectoryDescriptorRepository>();
        repository.FindAsync(parentId, false).Returns((DirectoryDescriptor)null);
        var manager = new DirectoryManager(repository, new ContainerNameValidator());

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            manager.CreateAsync(Guid.NewGuid(), "Default", "child", parentId));

        exception.Code.ShouldBe(FileExplorerErrorCodes.Directories.DirectoryNotExist);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectParentFromAnotherOwnerOrContainer()
    {
        var userId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var parent = new DirectoryDescriptor(parentId, "Other", "parent", null, 0, null)
        {
            CreatorId = Guid.NewGuid()
        };
        var repository = Substitute.For<IDirectoryDescriptorRepository>();
        repository.FindAsync(parentId, false).Returns(parent);
        var manager = new DirectoryManager(repository, new ContainerNameValidator());

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            manager.CreateAsync(userId, "Default", "child", parentId));

        exception.Code.ShouldBe(FileExplorerErrorCodes.Directories.DirectoryNotExist);
    }

    [Fact]
    public async Task EnsureEmptyAsync_ShouldRejectDirectoriesWithFiles()
    {
        var directory = CreateDirectory(Guid.NewGuid(), null);
        var repository = Substitute.For<IDirectoryDescriptorRepository>();
        repository.GetListAsync(directory.CreatorId.Value, directory.ContainerName, directory.Id)
            .Returns(new List<DirectoryDescriptor>());
        var fileRepository = Substitute.For<IFileDescriptorRepository>();
        fileRepository.GetListAsync(directory.ContainerName, null, directory.Id, maxResultCount: 1)
            .Returns(new List<FileDescriptor> { new FileDescriptor(Guid.NewGuid(), "Default", "blob", "file", "text/plain", string.Empty, directory.Id, string.Empty, null) });
        var manager = new DirectoryManager(repository, new ContainerNameValidator(), fileRepository);

        var exception = await Should.ThrowAsync<DirectoryNotEmptyException>(() =>
            manager.EnsureEmptyAsync(directory));

        exception.Code.ShouldBe(FileExplorerErrorCodes.Directories.DirectoryNotEmpty);
    }

    private static DirectoryDescriptor CreateDirectory(Guid id, Guid? parentId)
    {
        return new DirectoryDescriptor(id, "Default", id.ToString(), parentId, 0, null)
        {
            CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        };
    }
}
