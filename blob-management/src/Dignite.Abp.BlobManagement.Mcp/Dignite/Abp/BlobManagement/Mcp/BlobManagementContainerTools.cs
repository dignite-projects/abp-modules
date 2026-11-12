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
        "Lists the containers files can be uploaded to. Each has its name (what the other drive_* tools take as " +
        "containerName), the largest file it accepts (maxFileSize, bytes) and the file extensions it accepts (empty: any).")]
    [Authorize]
    public virtual async Task<IReadOnlyList<BlobManagementContainerDto>> ListContainersAsync()
    {
        return (await ContainerAppService.GetListAsync()).Items;
    }
}
