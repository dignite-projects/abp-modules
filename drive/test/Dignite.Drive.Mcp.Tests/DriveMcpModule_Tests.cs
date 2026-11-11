using System.Linq;
using Dignite.Abp.AspNetCore.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Drive.Mcp;

/// <summary>
/// The tools as <see cref="DriveMcpModule"/> registers them: inside the <c>drive</c> namespace, which the server checks at
/// startup and refuses to start without, and annotated for the client.
/// </summary>
public class DriveMcpModule_Tests
{
    [Fact]
    public void The_Tools_Should_Be_Registered_Inside_The_Drive_Namespace()
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
            "drive_create_upload_url",
            "drive_delete_file",
            "drive_get_file",
            "drive_list_containers",
            "drive_upload_file"
        });
    }

    [Fact]
    public void Read_Only_And_Destructive_Tools_Should_Say_So()
    {
        using var provider = BuildProvider();
        var tools = provider.GetServices<McpServerTool>().ToDictionary(tool => tool.ProtocolTool.Name);

        foreach (var name in new[] { "drive_list_containers", "drive_get_file" })
        {
            tools[name].ProtocolTool.Annotations!.ReadOnlyHint.ShouldBe(true);
        }

        tools["drive_delete_file"].ProtocolTool.Annotations!.DestructiveHint.ShouldBe(true);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        new DriveMcpModule().ConfigureServices(new ServiceConfigurationContext(services));
        return services.BuildServiceProvider();
    }
}
