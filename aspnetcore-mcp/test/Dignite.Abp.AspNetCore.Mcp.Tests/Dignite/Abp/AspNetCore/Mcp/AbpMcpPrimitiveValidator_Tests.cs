using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// The namespace rules, checked without a web host: each case builds only a service collection.
/// </summary>
public class AbpMcpPrimitiveValidator_Tests
{
    [Fact]
    public void Should_Accept_Modules_That_Stay_In_Their_Namespaces()
    {
        Should.NotThrow(() => Validate(services =>
        {
            services.AddAbpMcpModule("alpha", mcp => mcp.AddTools<AlphaTools>().AddResources<AlphaResources>());
            services.AddAbpMcpModule("beta", mcp => mcp.AddTools<BetaTools>());
        }));
    }

    [Fact]
    public void Should_Reject_A_Tool_Outside_Its_Module_Prefix()
    {
        var exception = Should.Throw<AbpException>(() => Validate(services =>
            services.AddAbpMcpModule("alpha", mcp => mcp.AddTools<UnprefixedTools>())));

        exception.Message.ShouldContain("'list_things'");
        exception.Message.ShouldContain("start with 'alpha_'");
    }

    [Fact]
    public void Should_Reject_A_Name_Registered_Twice_And_Name_Both_Owners()
    {
        var exception = Should.Throw<AbpException>(() => Validate(services =>
            services.AddAbpMcpModule("alpha", mcp => mcp.AddTools<AlphaTools>().AddTools<AlphaToolsAgain>())));

        exception.Message.ShouldContain("'alpha_list' is registered 2 times");
        exception.Message.ShouldContain(nameof(AlphaTools) + "." + nameof(AlphaTools.List));
        exception.Message.ShouldContain(nameof(AlphaToolsAgain) + "." + nameof(AlphaToolsAgain.List));
    }

    [Fact]
    public void Should_Reject_A_Tool_Registered_Outside_Any_Module()
    {
        var exception = Should.Throw<AbpException>(() => Validate(services =>
            services.AddMcpServer().WithTools<BetaTools>()));

        exception.Message.ShouldContain("'beta_list'");
        exception.Message.ShouldContain("outside any MCP module");
    }

    [Fact]
    public void Should_Reject_Nested_Module_Names()
    {
        var exception = Should.Throw<AbpException>(() => Validate(services =>
        {
            services.AddAbpMcpModule("file", mcp => mcp.AddInstructions("."));
            services.AddAbpMcpModule("file_explorer", mcp => mcp.AddInstructions("."));
        }));

        exception.Message.ShouldContain("'file_explorer' is nested inside the namespace of MCP module 'file'");
    }

    [Fact]
    public void Should_Reject_Two_Modules_Sharing_A_Uri_Scheme()
    {
        var exception = Should.Throw<AbpException>(() => Validate(services =>
        {
            services.AddAbpMcpModule("alpha", mcp => mcp.AddInstructions("."));
            services.AddAbpMcpModule("beta", mcp => mcp.UseUriScheme("alpha"));
        }));

        exception.Message.ShouldContain("use the URI scheme 'alpha'");
    }

    [Fact]
    public void Should_Reject_A_Resource_Outside_Its_Module_Scheme()
    {
        var exception = Should.Throw<AbpException>(() => Validate(services =>
            services.AddAbpMcpModule("beta", mcp => mcp.AddResources<AlphaResources>())));

        exception.Message.ShouldContain("'alpha://things'");
        exception.Message.ShouldContain("use the 'beta:' scheme");
    }

    [Fact]
    public void Should_Default_The_Uri_Scheme_To_The_Name_With_Hyphens()
    {
        Should.NotThrow(() => Validate(services =>
            services.AddAbpMcpModule("file_explorer", mcp => mcp.AddResources<FileExplorerResources>())));
    }

    [Theory]
    [InlineData("File")]
    [InlineData("file-explorer")]
    [InlineData("file.explorer")]
    [InlineData("_file")]
    [InlineData("file_")]
    public void Should_Reject_A_Module_Name_That_Is_Not_Snake_Case(string name)
    {
        Should.Throw<AbpException>(() => new ServiceCollection().AddAbpMcpModule(name, _ => { }));
    }

    [Fact]
    public void Should_Reject_A_Type_With_Nothing_To_Register()
    {
        var exception = Should.Throw<AbpException>(() =>
            new ServiceCollection().AddAbpMcpModule("alpha", mcp => mcp.AddTools<AlphaResources>()));

        exception.Message.ShouldContain("no method marked with [McpServerTool]");
    }

    /// <summary>
    /// Guards the trap <see cref="AbpMcpPrimitiveCache"/> documents: tagging ownership through the SDK's
    /// Metadata option would drop the method's attributes, and with them [Authorize].
    /// </summary>
    [Fact]
    public void Should_Keep_The_Authorize_Attribute_In_The_Tool_Metadata()
    {
        var services = new ServiceCollection();
        services.AddAbpMcpModule("alpha", mcp => mcp.AddTools<AlphaTools>());
        using var provider = services.BuildServiceProvider();

        var tool = provider.GetServices<McpServerTool>().Single(tool => tool.ProtocolTool.Name == "alpha_secret");

        tool.Metadata.OfType<AuthorizeAttribute>().ShouldHaveSingleItem().Policy.ShouldBe("Alpha.Secret");
    }

    [Fact]
    public void Should_Accept_A_Tool_Outside_Any_Module_When_The_Host_Allows_It()
    {
        Should.NotThrow(() => Validate(
            services => services.AddMcpServer().WithTools<BetaTools>(),
            allowUnownedPrimitives: true));
    }

    [Fact]
    public void Should_Still_Reject_Duplicates_When_Unowned_Tools_Are_Allowed()
    {
        var exception = Should.Throw<AbpException>(() => Validate(
            services =>
            {
                services.AddAbpMcpModule("beta", mcp => mcp.AddTools<BetaTools>());
                services.AddMcpServer().WithTools<BetaTools>();
            },
            allowUnownedPrimitives: true));

        exception.Message.ShouldContain("'beta_list' is registered 2 times");
    }

    private static void Validate(Action<IServiceCollection> configure, bool allowUnownedPrimitives = false)
    {
        var services = new ServiceCollection();
        configure(services);
        if (services.GetSingletonInstanceOrNull<AbpMcpModuleRegistry>() == null)
        {
            services.AddSingleton(new AbpMcpModuleRegistry());
        }
        services.TryAddSingleton<AbpMcpPrimitiveCache>();

        using var provider = services.BuildServiceProvider();
        new AbpMcpPrimitiveValidator(
                provider,
                provider.GetRequiredService<AbpMcpModuleRegistry>(),
                provider.GetRequiredService<AbpMcpPrimitiveCache>(),
                Options.Create(new AbpMcpServerOptions { AllowUnownedPrimitives = allowUnownedPrimitives }))
            .Validate();
    }

    [McpServerToolType]
    public class AlphaTools
    {
        [McpServerTool(Name = "alpha_list")]
        public string List() => "alpha";

        [McpServerTool(Name = "alpha_secret")]
        [Authorize("Alpha.Secret")]
        public string Secret() => "secret";
    }

    [McpServerToolType]
    public class AlphaToolsAgain
    {
        [McpServerTool(Name = "alpha_list")]
        public string List() => "alpha again";
    }

    [McpServerToolType]
    public class BetaTools
    {
        [McpServerTool(Name = "beta_list")]
        public string List() => "beta";
    }

    [McpServerToolType]
    public class UnprefixedTools
    {
        [McpServerTool(Name = "list_things")]
        public string List() => "things";
    }

    [McpServerResourceType]
    public class AlphaResources
    {
        [McpServerResource(UriTemplate = "alpha://things", Name = "alpha_things")]
        public string Things() => "things";
    }

    [McpServerResourceType]
    public class FileExplorerResources
    {
        [McpServerResource(UriTemplate = "file-explorer://directories", Name = "file_explorer_directories")]
        public string Directories() => "directories";
    }
}
