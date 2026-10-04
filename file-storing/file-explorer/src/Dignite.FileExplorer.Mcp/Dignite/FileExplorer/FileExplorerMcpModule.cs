using Dignite.Abp.AspNetCore.Mcp;
using Dignite.FileExplorer.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.Modularity;

namespace Dignite.FileExplorer;

/// <summary>
/// Contributes File Explorer's tools to the application's MCP server, under the <c>file_explorer</c>
/// namespace.
/// <para>
/// <b>Nothing is exposed until the host says so.</b> Blob containers cannot be enumerated, and which of
/// them an AI client should touch is a deployment decision, so the host lists them in
/// <see cref="FileExplorerMcpOptions.Containers"/>. The tools still go through the application services
/// for every call, so each container's own authorization configuration applies exactly as it does over
/// HTTP; the list is an additional gate, not a replacement for it.
/// </para>
/// </summary>
[DependsOn(
    typeof(FileExplorerApplicationContractsModule),
    typeof(AbpAspNetCoreMcpModule)
)]
public class FileExplorerMcpModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpMcpModule(FileExplorerMcpConsts.ModuleName, mcp => mcp
            .AddTools<ContainerTools>()
            .AddTools<DirectoryTools>()
            .AddTools<FileTools>()
            .AddInstructions(FileExplorerMcpConsts.Instructions));

        // Upload bytes travel base64-encoded in the request body, so the endpoint's body limit has to make
        // room for MaxUploadSize - while still refusing anything larger before it is read.
        context.Services.AddTransient<IPostConfigureOptions<AbpMcpServerOptions>, FileExplorerMcpServerOptionsSetup>();
    }
}
