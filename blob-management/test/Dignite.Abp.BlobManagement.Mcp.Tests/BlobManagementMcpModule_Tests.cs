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
/// The tools as <see cref="BlobManagementMcpModule"/> registers them: inside the <c>drive</c> namespace, which the server checks at
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
            "blob_create_upload_url",
            "blob_delete_file",
            "blob_get_file",
            "blob_list_containers",
            "blob_upload_file"
        });
    }

    [Fact]
    public void Read_Only_And_Destructive_Tools_Should_Say_So()
    {
        using var provider = BuildProvider();
        var tools = provider.GetServices<McpServerTool>().ToDictionary(tool => tool.ProtocolTool.Name);

        foreach (var name in new[] { "blob_list_containers", "blob_get_file" })
        {
            tools[name].ProtocolTool.Annotations!.ReadOnlyHint.ShouldBe(true);
        }

        tools["blob_delete_file"].ProtocolTool.Annotations!.DestructiveHint.ShouldBe(true);
    }

    [Fact]
    public void The_Instructions_Should_Not_Say_What_No_Tool_Does()
    {
        // Another package may add tools to the drive module, and AddInstructions only appends: a sentence claiming that
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
