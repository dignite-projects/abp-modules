using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Authorization;
using Dignite.Abp.BlobManagement.Features;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Features;

namespace Dignite.Abp.BlobManagement.Directories;

/// <summary>
/// The directories of the containers that allow them, for users. Authorization is <see cref="BlobManagementAccessService"/>'s,
/// and it is the container's: a caller who sees a container's tree lists its directories, and creates, renames and
/// moves, or deletes them by the permission for it - whichever directory it is. The tree rules - sibling names, cycles,
/// staying in one container, deleting only when empty - are <see cref="DirectoryManager"/>'s. Part of the tenant's Blob
/// Management: with <see cref="BlobManagementFeatures.Enable"/> off it refuses every call.
/// </summary>
[Authorize]
[RequiresFeature(BlobManagementFeatures.Enable)]
public class BlobManagementDirectoryAppService : BlobManagementAppService, IBlobManagementDirectoryAppService
{
    protected DirectoryManager DirectoryManager { get; }

    public BlobManagementDirectoryAppService(DirectoryManager directoryManager)
    {
        DirectoryManager = directoryManager;
    }

    public virtual async Task<BlobManagementDirectoryDto> GetAsync(Guid id)
    {
        GetUserId();
        var directory = await GetDirectoryAsync(id);
        Authorize((await GetAccessAsync(directory.ContainerName)).View);

        return Map(directory, await DirectoryRepository.HasChildrenAsync(directory.Id));
    }

    public virtual async Task<ListResultDto<BlobManagementDirectoryDto>> GetListAsync(GetBlobManagementDirectoriesInput input)
    {
        GetUserId();
        DirectoryManager.CheckDirectoriesAllowed(input.ContainerName);
        Authorize((await GetAccessAsync(input.ContainerName)).View);

        if (input.ParentId.HasValue)
        {
            await GetDirectoryAsync(input.ParentId.Value, input.ContainerName);
        }

        var children = await DirectoryRepository.GetChildrenAsync(input.ContainerName, input.ParentId);
        var withChildren = children.Count == 0
            ? new HashSet<Guid>()
            : new HashSet<Guid>(await DirectoryRepository.GetIdsWithChildrenAsync(children.Select(d => d.Id)));

        return new ListResultDto<BlobManagementDirectoryDto>(children
            .Select(d => Map(d, withChildren.Contains(d.Id)))
            .ToList());
    }

    public virtual async Task<BlobManagementDirectoryDto> CreateAsync(CreateBlobManagementDirectoryInput input)
    {
        GetUserId();
        DirectoryManager.CheckDirectoriesAllowed(input.ContainerName);
        Authorize((await GetAccessAsync(input.ContainerName)).CreateDirectories);

        if (input.ParentId.HasValue)
        {
            await GetDirectoryAsync(input.ParentId.Value, input.ContainerName);
        }

        var directory = await DirectoryManager.CreateAsync(input.ContainerName, input.Name.Trim(), input.ParentId);
        return Map(directory, hasChildren: false);
    }

    public virtual async Task<BlobManagementDirectoryDto> UpdateAsync(Guid id, UpdateBlobManagementDirectoryInput input)
    {
        GetUserId();
        var directory = await GetDirectoryAsync(id);
        Authorize((await GetAccessAsync(directory.ContainerName)).UpdateDirectories);

        await DirectoryManager.RenameAsync(directory, input.Name.Trim());
        return Map(directory, await DirectoryRepository.HasChildrenAsync(directory.Id));
    }

    public virtual async Task<BlobManagementDirectoryDto> MoveAsync(Guid id, MoveBlobManagementDirectoryInput input)
    {
        GetUserId();
        var directory = await GetDirectoryAsync(id);
        Authorize((await GetAccessAsync(directory.ContainerName)).UpdateDirectories);

        if (input.ParentId.HasValue && input.ParentId != directory.ParentId)
        {
            await GetDirectoryAsync(input.ParentId.Value, directory.ContainerName);
        }

        await DirectoryManager.MoveAsync(directory, input.ParentId, input.Order);
        return Map(directory, await DirectoryRepository.HasChildrenAsync(directory.Id));
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        GetUserId();
        var directory = await DirectoryRepository.FindAsync(id);
        if (directory == null || !IsManagedContainer(directory.ContainerName))
        {
            return;
        }

        Authorize((await GetAccessAsync(directory.ContainerName)).DeleteDirectories);
        await DirectoryManager.DeleteAsync(directory);
    }

    /// <summary>A directory of a managed container that allows directories; any other is not found.</summary>
    protected virtual async Task<DirectoryDescriptor> GetDirectoryAsync(Guid id)
    {
        var directory = await DirectoryRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(DirectoryDescriptor), id);
        return await GetDirectoryAsync(id, directory.ContainerName);
    }

    /// <summary>What the caller may do with the directories of <paramref name="containerName"/>.</summary>
    protected virtual async Task<BlobManagementAccess> GetAccessAsync(string containerName)
    {
        return await AccessService.GetAccessAsync(await AccessService.BeginAsync(), containerName);
    }

    protected virtual BlobManagementDirectoryDto Map(DirectoryDescriptor directory, bool hasChildren)
    {
        return new BlobManagementDirectoryDto
        {
            Id = directory.Id,
            CreationTime = directory.CreationTime,
            CreatorId = directory.CreatorId,
            ContainerName = directory.ContainerName,
            Name = directory.Name,
            ParentId = directory.ParentId,
            Order = directory.Order,
            HasChildren = hasChildren
        };
    }
}
