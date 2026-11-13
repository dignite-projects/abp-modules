using Dignite.Abp.AspNetCore.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobManagement.Mcp;

/// <summary>
/// Blob Management's MCP tools (<c>blob_*</c>), contributed to the application's MCP server: an assistant browses the
/// containers and their directories, and uploads, reads, renames, moves and deletes files for the signed-in user
/// through the application services, so the same authorization and rules apply as to the HTTP API. In-process or
/// through <c>Dignite.Abp.BlobManagement.HttpApi.Client</c>, as the host installs the module.
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
            .AddTools<BlobManagementDirectoryTools>()
            .AddTools<BlobManagementFileTools>()
            .AddInstructions(
                "Use the blob_* tools to work with the files of the BLOB containers. Start with blob_list_containers: " +
                "it lists the containers you have something to do in and what each accepts. A container with " +
                "allowDirectories has a directory tree: blob_list_directories and blob_list_files browse it one " +
                "directory at a time (omit the directory for the top of the container), and blob_create_directory adds " +
                "a directory. To add a real file, call blob_create_upload_url and run the command it returns on the " +
                "machine the file is on - the bytes never pass through you; blob_upload_file takes base64 and suits only " +
                "a file of a few kilobytes. Both take the directory to file it in. Keep the id an upload returns: " +
                "blob_get_file, blob_update_file (rename, move) and blob_delete_file take it."));
    }
}
