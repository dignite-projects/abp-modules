using System.Linq;
using Dignite.Abp.AspNetCore.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.FileExplorer.Mcp;

public class FileExplorerMcpModule_Tests
{
    /// <summary>
    /// Runs the same startup check the MCP server does, against this module's registrations alone - so a
    /// tool named outside <c>file_explorer_</c> fails here, in this module's own build, rather than in
    /// whichever host first combines it with another module.
    /// </summary>
    [Fact]
    public void Should_Register_Its_Tools_Inside_The_File_Explorer_Namespace()
    {
        var services = new ServiceCollection();
        new FileExplorerMcpModule().ConfigureServices(new ServiceConfigurationContext(services));
        using var provider = services.BuildServiceProvider();

        new AbpMcpPrimitiveValidator(
                provider,
                provider.GetRequiredService<AbpMcpModuleRegistry>(),
                provider.GetRequiredService<AbpMcpPrimitiveCache>(),
                Options.Create(new AbpMcpServerOptions()))
            .Validate();

        provider.GetServices<McpServerTool>().Select(tool => tool.ProtocolTool.Name).OrderBy(name => name).ShouldBe(new[]
        {
            "file_explorer_create_directory",
            "file_explorer_delete_file",
            "file_explorer_get_file",
            "file_explorer_list_containers",
            "file_explorer_list_directories",
            "file_explorer_list_files",
            "file_explorer_update_file",
            "file_explorer_upload_file"
        });
    }

    /// <summary>
    /// The module, not just the setup class, must raise the endpoint's body limit: without the registration
    /// /mcp stays at the 4 MB default and any upload past about 3 MB of file data is refused with 413.
    /// </summary>
    [Fact]
    public void Should_Raise_The_Mcp_Endpoint_Body_Limit_To_Fit_Its_Uploads()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        new FileExplorerMcpModule().ConfigureServices(new ServiceConfigurationContext(services));
        using var provider = services.BuildServiceProvider();

        var maxUploadSize = provider.GetRequiredService<IOptions<FileExplorerMcpOptions>>().Value.MaxUploadSize;
        provider.GetRequiredService<IOptions<AbpMcpServerOptions>>().Value.MaxRequestBodySize
            .ShouldBe(4 * ((maxUploadSize + 2) / 3) + FileExplorerMcpServerOptionsSetup.EnvelopeAllowance);
    }

    [Fact]
    public void Should_Mark_Read_Only_And_Destructive_Tools()
    {
        var services = new ServiceCollection();
        new FileExplorerMcpModule().ConfigureServices(new ServiceConfigurationContext(services));
        using var provider = services.BuildServiceProvider();
        var tools = provider.GetServices<McpServerTool>().ToDictionary(tool => tool.ProtocolTool.Name);

        tools["file_explorer_list_files"].ProtocolTool.Annotations!.ReadOnlyHint.ShouldBe(true);
        tools["file_explorer_delete_file"].ProtocolTool.Annotations!.DestructiveHint.ShouldBe(true);
    }
}
