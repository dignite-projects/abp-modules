using Dignite.Abp.AspNetCore.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobManagement.Mcp;

/// <summary>
/// The BlobManagement's MCP tools (<c>drive_*</c>), contributed to the application's MCP server: an assistant uploads, reads and
/// deletes files for the signed-in user through the file application services, so the same authorization and rules
/// apply as to the HTTP API. In-process or through <c>Dignite.Abp.BlobManagement.HttpApi.Client</c>, as the host installs the
/// BlobManagement.
/// </summary>
[DependsOn(
    typeof(BlobManagementApplicationContractsModule),
    typeof(AbpAspNetCoreMcpModule)
    )]
public class BlobManagementMcpModule : AbpModule
{
    public const string McpModuleName = "blob";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpMcpModule(McpModuleName, mcp => mcp
            .AddTools<BlobManagementContainerTools>()
            .AddTools<BlobManagementFileTools>()
            .AddInstructions(
                "Use the drive_* tools to add files to the BlobManagement and to read or delete a file by its id. Start with " +
                "blob_list_containers: it lists the containers you can upload to and what each accepts. To add a real " +
                "file, call blob_create_upload_url and run the command it returns on the machine the file is on - the " +
                "bytes never pass through you. blob_upload_file takes base64 and suits only a file of a few kilobytes. " +
                "Keep the id an upload returns: blob_get_file and blob_delete_file take it."));
    }
}
