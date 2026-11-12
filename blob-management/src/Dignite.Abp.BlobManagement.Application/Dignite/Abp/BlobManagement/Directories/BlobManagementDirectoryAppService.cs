using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Authorization;
using Dignite.Abp.BlobManagement.Features;
using Dignite.Abp.BlobManagement.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Features;

namespace Dignite.Abp.BlobManagement.Directories;

/// <summary>
/// The directories of the containers that allow them. Authorization is <see cref="BlobManagementAccessService"/>'s:
/// a directory is listed, renamed, moved and deleted by the permission of the place it is in, by a caller who may view
/// it, and directories are created in it by the permission of the place it is. The tree rules - sibling names, cycles,
/// staying in one container, deleting only when empty - are <see cref="DirectoryManager"/>'s. Part of the tenant's
/// Blob Management: with <see cref="BlobManagementFeatures.Enable"/> off it refuses every call.
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
        var place = await GetPlaceAsync(directory);
        Authorize(place.Access.View);

        return await MapAsync(directory, place);
    }

    /// <summary>
    /// The directories of the level the caller may view: every one that shares the level's own scope when the caller
    /// views the level, and each restricted one the caller is granted on - also at the top of a container the caller
    /// does not otherwise see, so that a directory granted there can be reached.
    /// </summary>
    public virtual async Task<ListResultDto<BlobManagementDirectoryDto>> GetListAsync(GetBlobManagementDirectoriesInput input)
    {
        GetUserId();
        DirectoryManager.CheckDirectoriesAllowed(input.ContainerName);

        var context = await AccessService.BeginAsync();
        var parent = input.ParentId.HasValue ? await GetDirectoryAsync(input.ParentId.Value, input.ContainerName) : null;
        var parentAccess = await AccessService.GetAccessAsync(context, input.ContainerName, parent);
        if (parent != null)
        {
            Authorize(parentAccess.View);
        }

        var children = await DirectoryRepository.GetChildrenAsync(input.ContainerName, input.ParentId);
        var access = await AccessService.GetChildrenAccessAsync(context, input.ContainerName, parentAccess, children);
        var visible = children.Where(d => access[d.Id].View).ToList();
        var withChildren = visible.Count == 0
            ? new HashSet<Guid>()
            : new HashSet<Guid>(await DirectoryRepository.GetIdsWithChildrenAsync(visible.Select(d => d.Id)));

        return new ListResultDto<BlobManagementDirectoryDto>(visible
            .Select(d => Map(d, access[d.Id], parentAccess, withChildren.Contains(d.Id)))
            .ToList());
    }

    public virtual async Task<BlobManagementDirectoryDto> CreateAsync(CreateBlobManagementDirectoryInput input)
    {
        GetUserId();
        DirectoryManager.CheckDirectoriesAllowed(input.ContainerName);

        var context = await AccessService.BeginAsync();
        var parent = input.ParentId.HasValue ? await GetDirectoryAsync(input.ParentId.Value, input.ContainerName) : null;
        var parentAccess = await AccessService.GetAccessAsync(context, input.ContainerName, parent);
        Authorize(parentAccess.CreateDirectories);

        var directory = await DirectoryManager.CreateAsync(input.ContainerName, input.Name.Trim(), input.ParentId);
        return Map(directory, parentAccess, parentAccess, hasChildren: false);
    }

    public virtual async Task<BlobManagementDirectoryDto> UpdateAsync(Guid id, UpdateBlobManagementDirectoryInput input)
    {
        GetUserId();
        var directory = await GetDirectoryAsync(id);
        var place = await GetPlaceAsync(directory);
        Authorize(place.Access.View && place.ParentAccess.UpdateDirectories);

        await DirectoryManager.RenameAsync(directory, input.Name.Trim());
        return await MapAsync(directory, place);
    }

    /// <summary>
    /// The caller needs to rename and move directories where the directory is, and to create directories where it
    /// lands. A restricted directory keeps its restriction, and with it who sees what is in it.
    /// </summary>
    public virtual async Task<BlobManagementDirectoryDto> MoveAsync(Guid id, MoveBlobManagementDirectoryInput input)
    {
        GetUserId();
        var directory = await GetDirectoryAsync(id);
        var place = await GetPlaceAsync(directory);
        Authorize(place.Access.View && place.ParentAccess.UpdateDirectories);

        if (input.ParentId != directory.ParentId)
        {
            var target = input.ParentId.HasValue
                ? await GetDirectoryAsync(input.ParentId.Value, directory.ContainerName)
                : null;
            Authorize((await AccessService.GetAccessAsync(place.Context, directory.ContainerName, target)).CreateDirectories);
        }

        await DirectoryManager.MoveAsync(directory, input.ParentId, input.Order);
        await CurrentUnitOfWork!.SaveChangesAsync();

        // Where it is now decides what may be done with it: read again, in a context that knows nothing of the old place.
        return await MapAsync(directory, await GetPlaceAsync(directory));
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        GetUserId();
        var directory = await DirectoryRepository.FindAsync(id);
        if (directory == null || !IsManagedContainer(directory.ContainerName))
        {
            return;
        }

        var place = await GetPlaceAsync(directory);
        Authorize(place.Access.View && place.ParentAccess.DeleteDirectories);
        await DirectoryManager.DeleteAsync(directory);
    }

    public virtual async Task<BlobManagementDirectoryDto> RestrictAsync(Guid id, RestrictBlobManagementDirectoryInput input)
    {
        GetUserId();
        await AuthorizationService.CheckAsync(BlobManagementPermissions.ManagePermissions);
        var directory = await GetDirectoryAsync(id);

        await DirectoryManager.SetRestrictedAsync(directory, input.IsRestricted);
        return await MapAsync(directory, await GetPlaceAsync(directory));
    }

    /// <summary>A directory of a managed container that allows directories; any other is not found.</summary>
    protected virtual async Task<DirectoryDescriptor> GetDirectoryAsync(Guid id)
    {
        var directory = await DirectoryRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(DirectoryDescriptor), id);
        return await GetDirectoryAsync(id, directory.ContainerName);
    }

    /// <summary>
    /// What the caller may do in <paramref name="directory"/>, and at the place it is in - its parent, or the top of
    /// its container - which is what renaming, moving and deleting it take.
    /// </summary>
    protected virtual async Task<DirectoryPlace> GetPlaceAsync(DirectoryDescriptor directory)
    {
        var context = await AccessService.BeginAsync();
        var parent = directory.ParentId.HasValue ? await DirectoryRepository.FindAsync(directory.ParentId.Value) : null;
        return new DirectoryPlace(
            context,
            await AccessService.GetAccessAsync(context, directory.ContainerName, directory),
            await AccessService.GetAccessAsync(context, directory.ContainerName, parent));
    }

    protected virtual async Task<BlobManagementDirectoryDto> MapAsync(DirectoryDescriptor directory, DirectoryPlace place)
    {
        return Map(directory, place.Access, place.ParentAccess, await DirectoryRepository.HasChildrenAsync(directory.Id));
    }

    protected virtual BlobManagementDirectoryDto Map(
        DirectoryDescriptor directory,
        BlobManagementAccess access,
        BlobManagementAccess parentAccess,
        bool hasChildren)
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
            HasChildren = hasChildren,
            IsRestricted = directory.IsRestricted,
            CanUpdate = access.View && parentAccess.UpdateDirectories,
            CanDelete = access.View && parentAccess.DeleteDirectories,
            Access = access.ToDto()
        };
    }

    /// <summary>What the caller may do in a directory (<see cref="Access"/>) and at the place it is in (<see cref="ParentAccess"/>).</summary>
    protected class DirectoryPlace
    {
        public DirectoryPlace(BlobManagementAccessContext context, BlobManagementAccess access, BlobManagementAccess parentAccess)
        {
            Context = context;
            Access = access;
            ParentAccess = parentAccess;
        }

        public BlobManagementAccessContext Context { get; }

        public BlobManagementAccess Access { get; }

        public BlobManagementAccess ParentAccess { get; }
    }
}
