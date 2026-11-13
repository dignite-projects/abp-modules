using Dignite.Abp.AspNetCore.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Dignite.Abp.BlobManagement.Mcp;

/// <summary>
/// Blob Management's MCP tools (<c>blob_*</c>), contributed to the application's MCP server: an assistant reads what
/// the management page shows - the containers, their directories and their files - for the signed-in user, through the
/// application services, so the same authorization applies as to the HTTP API. Every tool reads; a file is changed by
/// the business system that keeps it. For the host that runs Blob Management: the file tools call
/// <c>IBlobManagementFileAppService</c>, which only <c>Dignite.Abp.BlobManagement.Application</c> registers - a host
/// that reaches Blob Management through <c>Dignite.Abp.BlobManagement.HttpApi.Client</c> calls it as a client, and a
/// client's reads are not a signed-in user's.
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
                "Use the blob_* tools to look at what the BLOB containers hold. A file is stored, renamed, moved and " +
                "deleted by the business system that keeps it, with that system's own tools. Start with " +
                "blob_list_containers: it lists the containers you have something to do in and what each accepts. A " +
                "container with allowDirectories has a directory tree: blob_list_directories and blob_list_files " +
                "browse it one directory at a time (omit the directory for the top of the container). blob_get_file " +
                "returns one file by the id a listing gave."));
    }
}
