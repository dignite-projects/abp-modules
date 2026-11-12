using Dignite.Abp.AspNetCore.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Dignite.Drive.Mcp;

/// <summary>
/// The Drive's MCP tools (<c>drive_*</c>), contributed to the application's MCP server: an assistant uploads, reads and
/// deletes files for the signed-in user through the file application services, so the same authorization and rules
/// apply as to the HTTP API. In-process or through <c>Dignite.Drive.HttpApi.Client</c>, as the host installs the
/// Drive.
/// </summary>
[DependsOn(
    typeof(DriveApplicationContractsModule),
    typeof(AbpAspNetCoreMcpModule)
    )]
public class DriveMcpModule : AbpModule
{
    public const string McpModuleName = "drive";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpMcpModule(McpModuleName, mcp => mcp
            .AddTools<DriveContainerTools>()
            .AddTools<DriveFileTools>()
            .AddInstructions(
                "Use the drive_* tools to add files to the Drive and to read or delete a file by its id. Start with " +
                "drive_list_containers: it lists the containers you can upload to and what each accepts. To add a real " +
                "file, call drive_create_upload_url and run the command it returns on the machine the file is on - the " +
                "bytes never pass through you. drive_upload_file takes base64 and suits only a file of a few kilobytes. " +
                "Keep the id an upload returns: drive_get_file and drive_delete_file take it."));
    }
}
