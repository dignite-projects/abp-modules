using System.Linq;
using Dignite.Abp.AspNetCore.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.BlobManagement.Mcp;

/// <summary>
/// The tools as <see cref="BlobManagementMcpModule"/> registers them: inside the <c>blob</c> namespace, which the server checks at
/// startup and refuses to start without, and annotated for the client.
/// </summary>
public class BlobManagementMcpModule_Tests
{
    [Fact]
    public void The_Tools_Should_Be_Registered_Inside_The_BlobManagement_Namespace()
    {
        using var provider = BuildProvider();

        new AbpMcpPrimitiveValidator(
                provider,
                provider.GetRequiredService<AbpMcpModuleRegistry>(),
                provider.GetRequiredService<AbpMcpPrimitiveCache>(),
                Options.Create(new AbpMcpServerOptions()))
            .Validate();

        provider.GetServices<McpServerTool>().Select(tool => tool.ProtocolTool.Name).OrderBy(name => name).ShouldBe(new[]
        {
            "blob_get_file",
            "blob_list_containers",
            "blob_list_directories",
            "blob_list_files"
        });
    }

    [Fact]
    public void Every_Tool_Should_Say_That_It_Only_Reads()
    {
        // A file is its business system's to change, and a directory a user's on the management page: these tools read.
        using var provider = BuildProvider();

        foreach (var tool in provider.GetServices<McpServerTool>())
        {
            tool.ProtocolTool.Annotations!.ReadOnlyHint.ShouldBe(true, tool.ProtocolTool.Name);
        }
    }

    [Fact]
    public void The_Instructions_Should_Not_Say_What_No_Tool_Does()
    {
        // Another package may add tools to the blob module, and AddInstructions only appends: a sentence claiming that
        // no tool does something would contradict the tools it adds.
        using var provider = BuildProvider();
        var instructions = string.Join(" ", provider.GetRequiredService<AbpMcpModuleRegistry>()
            .Find(BlobManagementMcpModule.McpModuleName)!.Instructions);

        instructions.ShouldContain("blob_get_file");
        instructions.ShouldNotContain("no tool", Case.Insensitive);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        new BlobManagementMcpModule().ConfigureServices(new ServiceConfigurationContext(services));
        return services.BuildServiceProvider();
    }
}
