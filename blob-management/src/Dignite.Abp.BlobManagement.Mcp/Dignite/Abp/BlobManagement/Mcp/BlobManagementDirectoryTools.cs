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
/// Browsing directories. Creating, renaming, moving and deleting them is left to the management page and to the
/// business system that keeps its files in them: the tools of this module read.
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
        "Lists the directories directly under a directory, or at the top of a container - for a container whose " +
        "allowDirectories is true and whose directory tree you may see. Each has its id and hasChildren (whether to " +
        "look further down).")]
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
}
