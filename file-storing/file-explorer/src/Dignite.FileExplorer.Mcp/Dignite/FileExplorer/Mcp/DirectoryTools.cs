using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Dignite.FileExplorer.Directories;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.FileExplorer.Mcp;

/// <summary>
/// Reading the directory tree and adding to it. Moving, renaming and deleting directories are left to the
/// HTTP API and its UI: a directory carries other files with it, which is a larger change than an
/// assistant should make in passing.
/// </summary>
[McpServerToolType]
public class DirectoryTools : ITransientDependency
{
    protected IDirectoryDescriptorAppService DirectoryAppService { get; }

    protected FileExplorerMcpContainerGuard ContainerGuard { get; }

    public DirectoryTools(IDirectoryDescriptorAppService directoryAppService, FileExplorerMcpContainerGuard containerGuard)
    {
        DirectoryAppService = directoryAppService;
        ContainerGuard = containerGuard;
    }

    [McpServerTool(Name = "file_explorer_list_directories", Title = "List directories", ReadOnly = true)]
    [Description("Returns the current user's directory tree in a container. Each directory carries its id and its children.")]
    public virtual async Task<IReadOnlyList<DirectoryDescriptorInfoDto>> ListDirectoriesAsync(
        [Description("A container name from file_explorer_list_containers.")]
        string containerName)
    {
        var container = ContainerGuard.GetContainer(containerName);
        var result = await DirectoryAppService.GetListAsync(new GetDirectoriesInput { ContainerName = container.Name });
        return result.Items;
    }

    [McpServerTool(Name = "file_explorer_create_directory", Title = "Create a directory")]
    [Description("Creates a directory in a container, at the root or under an existing directory.")]
    public virtual async Task<DirectoryDescriptorDto> CreateDirectoryAsync(
        [Description("A container name from file_explorer_list_containers.")]
        string containerName,
        [Description("The new directory's name.")]
        string name,
        [Description("The id of the directory to create it under, from file_explorer_list_directories. Omit for the container's root.")]
        Guid? parentId = null)
    {
        var container = ContainerGuard.GetContainer(containerName);
        return await DirectoryAppService.CreateAsync(new CreateDirectoryInput
        {
            ContainerName = container.Name,
            Name = name,
            ParentId = parentId
        });
    }
}
