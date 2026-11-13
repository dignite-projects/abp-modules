using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Containers;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobManagement.Mcp;

[McpServerToolType]
public class BlobManagementContainerTools : ITransientDependency
{
    protected IBlobManagementContainerAppService ContainerAppService { get; }

    public BlobManagementContainerTools(IBlobManagementContainerAppService containerAppService)
    {
        ContainerAppService = containerAppService;
    }

    [McpServerTool(Name = "blob_list_containers", Title = "List containers", ReadOnly = true)]
    [Description(
        "Lists the containers you have something to do in. Each has its name (what the other blob_* tools take as " +
        "containerName), the largest file it accepts (maxFileSize, bytes), the file extensions it accepts (empty: any), " +
        "whether it has directories (allowDirectories) and what you may do at its top (access).")]
    [Authorize]
    public virtual async Task<IReadOnlyList<BlobManagementContainerDto>> ListContainersAsync()
    {
        return (await ContainerAppService.GetListAsync()).Items;
    }
}
