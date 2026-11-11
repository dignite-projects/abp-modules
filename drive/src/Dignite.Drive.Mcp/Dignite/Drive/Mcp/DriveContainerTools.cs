using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Dignite.Drive.Containers;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Drive.Mcp;

[McpServerToolType]
public class DriveContainerTools : ITransientDependency
{
    protected IDriveContainerAppService ContainerAppService { get; }

    public DriveContainerTools(IDriveContainerAppService containerAppService)
    {
        ContainerAppService = containerAppService;
    }

    [McpServerTool(Name = "drive_list_containers", Title = "List containers", ReadOnly = true)]
    [Description(
        "Lists the containers files can be uploaded to. Each has its name (what the other drive_* tools take as " +
        "containerName), the largest file it accepts (maxFileSize, bytes) and the file extensions it accepts (empty: any).")]
    [Authorize]
    public virtual async Task<IReadOnlyList<DriveContainerDto>> ListContainersAsync()
    {
        return (await ContainerAppService.GetListAsync()).Items;
    }
}
