using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Dignite.FileExplorer.Files;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace Dignite.FileExplorer.Directories;

public class DirectoryManager : DomainService
{
    protected ContainerNameValidator ContainerNameValidator { get; }
    protected IDirectoryDescriptorRepository DirectoryDescriptorRepository { get; }
    protected IFileDescriptorRepository FileDescriptorRepository { get; }

    public DirectoryManager(
        IDirectoryDescriptorRepository directoryDescriptorRepository,
        ContainerNameValidator containerNameValidator,
        IFileDescriptorRepository fileDescriptorRepository = null)
    {
        DirectoryDescriptorRepository = directoryDescriptorRepository;
        ContainerNameValidator = containerNameValidator;
        FileDescriptorRepository = fileDescriptorRepository;
    }

    /// <summary>
    /// Directories must be empty before deletion. Children and files are not cascaded or reparented.
    /// </summary>
    public virtual async Task EnsureEmptyAsync(DirectoryDescriptor directory, CancellationToken cancellationToken = default)
    {
        if (!directory.CreatorId.HasValue || FileDescriptorRepository == null)
        {
            throw new DirectoryNotEmptyException();
        }

        var childDirectories = await DirectoryDescriptorRepository.GetListAsync(
            directory.CreatorId.Value,
            directory.ContainerName,
            directory.Id,
            cancellationToken);
        var files = await FileDescriptorRepository.GetListAsync(
            directory.ContainerName,
            null,
            directory.Id,
            maxResultCount: 1,
            cancellationToken: cancellationToken);

        if (childDirectories == null || files == null || childDirectories.Count != 0 || files.Count != 0)
        {
            throw new DirectoryNotEmptyException();
        }
    }

    /// <summary>
    /// Deletes a directory only after checking that it has no children or files.
    /// The application service executes this method inside a transactional unit of work;
    /// keeping the check and delete together prevents a successful check from being
    /// separated from the actual delete by another request.
    /// </summary>
    public virtual async Task DeleteAsync(
        DirectoryDescriptor directory,
        CancellationToken cancellationToken = default)
    {
        await EnsureEmptyAsync(directory, cancellationToken);
        await DirectoryDescriptorRepository.DeleteAsync(directory, cancellationToken: cancellationToken);
    }

    public virtual async Task<DirectoryDescriptor> CreateAsync(
        Guid userId,
        string containerName,
        string name,
        Guid? parentId = null,
        CancellationToken cancellationToken = default)
    {
        ContainerNameValidator.Validate(containerName);

        if (parentId.HasValue)
        {
            var parent = await DirectoryDescriptorRepository.FindAsync(parentId.Value, false, cancellationToken);
            if (parent == null ||
                !parent.ContainerName.Equals(containerName, StringComparison.OrdinalIgnoreCase) ||
                parent.TenantId != CurrentTenant.Id ||
                parent.CreatorId != userId)
            {
                throw new BusinessException(FileExplorerErrorCodes.Directories.DirectoryNotExist);
            }
        }

        //
        if (await DirectoryDescriptorRepository.NameExistsAsync(userId, containerName, name, parentId, cancellationToken))
        {
            throw new DirectoryAlreadyExistException(name);
        }

        //
        var order = await DirectoryDescriptorRepository.GetMaxOrderAsync(userId, containerName, parentId, cancellationToken);
        var directory = new DirectoryDescriptor(
            GuidGenerator.Create(),
            containerName, name, parentId,
            order + 1,
            CurrentTenant.Id
            );
        return await DirectoryDescriptorRepository.InsertAsync(directory, cancellationToken: cancellationToken);
    }

    public virtual async Task<DirectoryDescriptor> MoveAsync(
        DirectoryDescriptor directory,
        Guid? parentId,
        int order,
        CancellationToken cancellationToken = default)
    {
        if (parentId.HasValue)
        {
            var parent = await DirectoryDescriptorRepository.GetAsync(parentId.Value, cancellationToken: cancellationToken);
            if (!parent.ContainerName.Equals(directory.ContainerName, StringComparison.CurrentCultureIgnoreCase))
            {
                throw new DirectoryInvalidMoveException();
            }

            if (await IsDescendantOrSelfAsync(directory.Id, parent, cancellationToken))
            {
                throw new BusinessException(FileExplorerErrorCodes.Directories.ForbidMovingToChild);
            }
        }

        var children = await DirectoryDescriptorRepository.GetListAsync(
            directory.CreatorId.Value,
            directory.ContainerName,
            parentId,
            cancellationToken);
        foreach (var item in children.Where(d=>d.Order>=order && d.Id!=directory.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            item.SetOrder(item.Order + 1);
            await DirectoryDescriptorRepository.UpdateAsync(item, cancellationToken: cancellationToken);
        }

        directory.MoveTo(parentId, order);
        return await DirectoryDescriptorRepository.UpdateAsync(directory, cancellationToken: cancellationToken);
    }

    private async Task<bool> IsDescendantOrSelfAsync(
        Guid directoryId,
        DirectoryDescriptor parent,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid>();
        var current = parent;

        while (true)
        {
            if (current.Id == directoryId || !visited.Add(current.Id))
            {
                return true;
            }

            if (!current.ParentId.HasValue)
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            current = await DirectoryDescriptorRepository.GetAsync(
                current.ParentId.Value,
                cancellationToken: cancellationToken);
        }
    }

    public virtual async Task<DirectoryDescriptor> UpdateAsync(
        DirectoryDescriptor directory,
        string name,
        CancellationToken cancellationToken = default)
    {
        //
        if (!directory.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
        {
            if (await DirectoryDescriptorRepository.NameExistsAsync(
                    directory.CreatorId.Value,
                    directory.ContainerName,
                    name,
                    directory.ParentId,
                    cancellationToken))
            {
                throw new DirectoryAlreadyExistException(name);
            }
        }

        directory.Rename(name);
        return await DirectoryDescriptorRepository.UpdateAsync(directory, cancellationToken: cancellationToken);
    }
}
