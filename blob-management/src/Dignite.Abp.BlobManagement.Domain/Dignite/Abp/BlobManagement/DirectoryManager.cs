using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Services;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The rules that keep the directories of a container a tree. Sibling names are unique, a parent is in the same
/// container, a directory never ends up inside itself, the tree is at most
/// <see cref="DirectoryDescriptorConsts.MaxDepth"/> levels deep, every <see cref="DirectoryDescriptor.Path"/> names the
/// directory's actual ancestors, and only an empty directory is deleted. A container has directories only when its
/// configuration allows them (<see cref="BlobManagementContainerConfiguration.AllowDirectories"/>).
/// <para>
/// It authorizes nobody: the application services decide who may do what.
/// </para>
/// <para>
/// Sibling names are checked here, against the siblings read from the database - as ABP checks the display name of an
/// organization unit (<c>OrganizationUnitManager.ValidateOrganizationUnitAsync</c>) - and not by a unique index. The
/// check is check-then-act: two simultaneous requests can leave two siblings of one name. A directory is known by its
/// id, so that costs a duplicate label, not a wrong file.
/// </para>
/// </summary>
public class DirectoryManager : DomainService
{
    protected IDirectoryDescriptorRepository DirectoryRepository { get; }

    protected IFileDescriptorRepository FileRepository { get; }

    protected IBlobContainerConfigurationProvider BlobContainerConfigurationProvider { get; }

    public DirectoryManager(
        IDirectoryDescriptorRepository directoryRepository,
        IFileDescriptorRepository fileRepository,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider)
    {
        DirectoryRepository = directoryRepository;
        FileRepository = fileRepository;
        BlobContainerConfigurationProvider = blobContainerConfigurationProvider;
    }

    /// <summary>
    /// Creates a directory under <paramref name="parentId"/> (<c>null</c>: the top of the container), at most
    /// <see cref="DirectoryDescriptorConsts.MaxDepth"/> levels down.
    /// </summary>
    public virtual async Task<DirectoryDescriptor> CreateAsync(
        string containerName,
        string name,
        Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name), DirectoryDescriptorConsts.MaxNameLength);
        CheckDirectoriesAllowed(containerName);

        var parent = parentId.HasValue
            ? await GetInContainerAsync(parentId.Value, containerName, cancellationToken)
            : null;
        if (parent != null && parent.GetDepth() + 1 > DirectoryDescriptorConsts.MaxDepth)
        {
            throw TooDeep();
        }

        await CheckNameIsFreeAsync(containerName, parentId, name, exceptId: null, cancellationToken);

        var order = await DirectoryRepository.GetMaxOrderAsync(containerName, parentId, cancellationToken);
        return await DirectoryRepository.InsertAsync(
            new DirectoryDescriptor(
                GuidGenerator.Create(), containerName, name, parentId, parent?.Path, order + 1, CurrentTenant.Id),
            autoSave: true,
            cancellationToken: cancellationToken);
    }

    public virtual async Task<DirectoryDescriptor> RenameAsync(
        DirectoryDescriptor directory,
        string name,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(directory, nameof(directory));
        Check.NotNullOrWhiteSpace(name, nameof(name), DirectoryDescriptorConsts.MaxNameLength);

        if (directory.Name == name)
        {
            return directory;
        }

        await CheckNameIsFreeAsync(directory.ContainerName, directory.ParentId, name, directory.Id, cancellationToken);

        directory.Rename(name);
        return await DirectoryRepository.UpdateAsync(directory, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Re-parents <paramref name="directory"/> under <paramref name="parentId"/> (<c>null</c>: the top of its
    /// container) at <paramref name="order"/>, shifting the siblings at or after that position down by one. The parent
    /// must be in the same container, and not the directory itself or one below it; the deepest directory of the moved
    /// subtree stays within <see cref="DirectoryDescriptorConsts.MaxDepth"/>. The <see cref="DirectoryDescriptor.Path"/>
    /// of every directory below it moves along.
    /// <para>
    /// The check against moving a directory into itself reads the parent once: two simultaneous moves of two
    /// directories into each other can both pass it. A host that expects such traffic serializes directory moves.
    /// </para>
    /// </summary>
    public virtual async Task<DirectoryDescriptor> MoveAsync(
        DirectoryDescriptor directory,
        Guid? parentId,
        int order,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(directory, nameof(directory));

        DirectoryDescriptor? parent = null;
        if (parentId.HasValue)
        {
            parent = await GetInContainerAsync(parentId.Value, directory.ContainerName, cancellationToken);
            if (parent.IsAtOrBelow(directory))
            {
                throw new BusinessException(BlobManagementErrorCodes.DirectoryCannotMoveIntoItself);
            }
        }

        // Only a new parent changes paths; a move among its own siblings is a change of position.
        var descendants = new List<DirectoryDescriptor>();
        if (parentId != directory.ParentId)
        {
            descendants = await DirectoryRepository.GetDescendantsAsync(directory.Path, cancellationToken);

            var depth = (parent?.GetDepth() ?? 0) + 1;
            var below = descendants.Count == 0 ? 0 : descendants.Max(d => d.GetDepth()) - directory.GetDepth();
            if (depth + below > DirectoryDescriptorConsts.MaxDepth)
            {
                throw TooDeep();
            }

            await CheckNameIsFreeAsync(directory.ContainerName, parentId, directory.Name, directory.Id, cancellationToken);
        }

        var siblings = await DirectoryRepository.GetChildrenAsync(directory.ContainerName, parentId, cancellationToken);
        foreach (var sibling in siblings.Where(d => d.Order >= order && d.Id != directory.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            sibling.SetOrder(sibling.Order + 1);
            await DirectoryRepository.UpdateAsync(sibling, cancellationToken: cancellationToken);
        }

        var oldPath = directory.Path;
        directory.MoveTo(parentId, parent?.Path, order);
        await DirectoryRepository.UpdateAsync(directory, cancellationToken: cancellationToken);

        foreach (var descendant in descendants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            descendant.ReplacePathPrefix(oldPath, directory.Path);
            await DirectoryRepository.UpdateAsync(descendant, cancellationToken: cancellationToken);
        }

        return directory;
    }

    /// <summary>
    /// Deletes an empty directory - soft, as ABP deletes a <c>FullAuditedAggregateRoot</c>; one with a subdirectory or a
    /// file is refused with <see cref="BlobManagementErrorCodes.DirectoryNotEmpty"/>. A file or a subdirectory that is
    /// deleted itself does not count: nobody lists it, and a file restored later whose directory is gone goes to the top
    /// of its container (<see cref="FileDescriptorManager.RestoreAsync"/>). The check and the delete are check-then-act:
    /// a file stored in the directory, or a subdirectory created in it, between the two is left naming a directory that
    /// is deleted.
    /// </summary>
    public virtual async Task DeleteAsync(DirectoryDescriptor directory, CancellationToken cancellationToken = default)
    {
        Check.NotNull(directory, nameof(directory));

        if (await DirectoryRepository.HasChildrenAsync(directory.Id, cancellationToken) ||
            await FileRepository.AnyInDirectoryAsync(directory.Id, cancellationToken))
        {
            throw new BusinessException(BlobManagementErrorCodes.DirectoryNotEmpty);
        }

        await DirectoryRepository.DeleteAsync(directory, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// The directories above <paramref name="directory"/>, nearest first, ending at the top of its tree. They are the
    /// ids in its <see cref="DirectoryDescriptor.Path"/>, read in one query; one that does not exist is simply absent.
    /// </summary>
    public virtual async Task<List<DirectoryDescriptor>> GetAncestorsAsync(
        DirectoryDescriptor directory,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(directory, nameof(directory));

        var ancestorIds = directory.GetAncestorIds();
        if (ancestorIds.Count == 0)
        {
            return new List<DirectoryDescriptor>();
        }

        var ancestors = (await DirectoryRepository.GetListByIdsAsync(ancestorIds, cancellationToken)).ToDictionary(d => d.Id);
        return ancestorIds.Where(ancestors.ContainsKey).Select(id => ancestors[id]).ToList();
    }

    /// <summary>
    /// The directory <paramref name="id"/> when it is a directory of <paramref name="containerName"/>; otherwise
    /// <see cref="BlobManagementErrorCodes.DirectoryNotFound"/>, without saying why.
    /// </summary>
    public virtual async Task<DirectoryDescriptor> GetInContainerAsync(
        Guid id,
        string containerName,
        CancellationToken cancellationToken = default)
    {
        var directory = await DirectoryRepository.FindAsync(id, false, cancellationToken);
        if (directory == null || directory.ContainerName != containerName)
        {
            throw new BusinessException(BlobManagementErrorCodes.DirectoryNotFound);
        }

        return directory;
    }

    /// <summary>
    /// Fails with <see cref="BlobManagementErrorCodes.ContainerNotFound"/> for a container that is not managed, and with
    /// <see cref="BlobManagementErrorCodes.DirectoriesNotAllowed"/> for one that does not allow directories.
    /// </summary>
    public virtual void CheckDirectoriesAllowed(string containerName)
    {
        if (!BlobContainerConfigurationProvider.GetBlobManagementContainerConfiguration(containerName).AllowDirectories)
        {
            throw new BusinessException(BlobManagementErrorCodes.DirectoriesNotAllowed)
                .WithData("ContainerName", containerName);
        }
    }

    /// <summary>
    /// Fails with <see cref="BlobManagementErrorCodes.DirectoryNameAlreadyExists"/> when a directory under
    /// <paramref name="parentId"/> other than <paramref name="exceptId"/> is named <paramref name="name"/>, as written.
    /// A deleted directory is not a sibling: its name is free.
    /// </summary>
    protected virtual async Task CheckNameIsFreeAsync(
        string containerName,
        Guid? parentId,
        string name,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        var siblings = await DirectoryRepository.GetChildrenAsync(containerName, parentId, cancellationToken);
        if (siblings.Any(d => d.Id != exceptId && string.Equals(d.Name, name, StringComparison.Ordinal)))
        {
            throw new BusinessException(BlobManagementErrorCodes.DirectoryNameAlreadyExists).WithData("Name", name);
        }
    }

    protected virtual BusinessException TooDeep()
    {
        return new BusinessException(BlobManagementErrorCodes.DirectoryTooDeep).WithData("MaxDepth", DirectoryDescriptorConsts.MaxDepth);
    }
}
