using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Directories;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobManagement.Mcp;

/// <summary>
/// Browsing directories and adding one. Renaming, moving and deleting directories, and their permissions, are left to
/// the management page: a directory carries its files with it, a larger change than an assistant should make in passing.
/// </summary>
[McpServerToolType]
public class BlobManagementDirectoryTools : ITransientDependency
{
    protected IBlobManagementDirectoryAppService DirectoryAppService { get; }

    public BlobManagementDirectoryTools(IBlobManagementDirectoryAppService directoryAppService)
    {
        DirectoryAppService = directoryAppService;
    }

    [McpServerTool(Name = "blob_list_directories", Title = "List directories", ReadOnly = true)]
    [Description(
        "Lists the directories directly under a directory, or at the top of a container, that you may see - for a " +
        "container whose allowDirectories is true. Each has its id, hasChildren (whether to look further down) and " +
        "access: what you may do in it (uploadFiles, createDirectories and the rest).")]
    [Authorize]
    public virtual async Task<IReadOnlyList<BlobManagementDirectoryDto>> ListDirectoriesAsync(
        [Description("A container's name, from blob_list_containers.")]
        string containerName,
        [Description("The directory to look into, by id. Omit for the top of the container.")]
        Guid? parentId = null)
    {
        return (await DirectoryAppService.GetListAsync(new GetBlobManagementDirectoriesInput
        {
            ContainerName = containerName,
            ParentId = parentId
        })).Items;
    }

    [McpServerTool(Name = "blob_create_directory", Title = "Create a directory")]
    [Description("Creates a directory at the top of a container or under a directory. Names are unique among siblings, whatever their case.")]
    [Authorize]
    public virtual Task<BlobManagementDirectoryDto> CreateDirectoryAsync(
        [Description("A container's name, from blob_list_containers.")]
        string containerName,
        [Description("The new directory's name.")]
        string name,
        [Description("The directory to create it in, by id from blob_list_directories. Omit for the top of the container.")]
        Guid? parentId = null)
    {
        return DirectoryAppService.CreateAsync(new CreateBlobManagementDirectoryInput
        {
            ContainerName = containerName,
            Name = name,
            ParentId = parentId
        });
    }
}
