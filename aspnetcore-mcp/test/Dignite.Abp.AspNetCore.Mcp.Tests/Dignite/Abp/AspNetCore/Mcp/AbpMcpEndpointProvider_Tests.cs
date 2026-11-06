using System;
using System.Linq;
using Dignite.Abp.AspNetCore.Mcp.Authentication;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Authentication;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// How endpoints are resolved from the options, and every way an endpoint configuration is refused - checked
/// without a web host.
/// </summary>
public class AbpMcpEndpointProvider_Tests
{
    [Fact]
    public void Should_Serve_Every_Module_At_One_Endpoint_When_Not_Split()
    {
        var provider = CreateProvider(_ => { });

        var endpoint = provider.Endpoints.ShouldHaveSingleItem();
        endpoint.RoutePattern.ShouldBe("/mcp");
        endpoint.IsDefault.ShouldBeTrue();
        endpoint.ServesEveryModule.ShouldBeTrue();
        endpoint.Instructions.ShouldBe("Opening.\n\nAlpha section.\n\nBeta section.");
        provider.IsSplit.ShouldBeFalse();
    }

    [Fact]
    public void Should_Give_Each_Endpoint_Its_Modules_And_Instructions()
    {
        var provider = CreateProvider(options =>
        {
            options.Modules.Add("alpha");
            options.Endpoints.Add(new AbpMcpEndpointOptions("/beta-mcp") { Modules = { "beta" }, Instructions = "Beta opening." });
        });

        provider.IsSplit.ShouldBeTrue();
        provider.Endpoints.Select(endpoint => endpoint.RoutePattern).ShouldBe(new[] { "/mcp", "/beta-mcp" });

        var defaultEndpoint = provider.DefaultEndpoint;
        defaultEndpoint.Modules!.Select(module => module.Name).ShouldBe(new[] { "alpha" });
        defaultEndpoint.Instructions.ShouldBe("Opening.\n\nAlpha section.");

        var beta = provider.Endpoints[1];
        beta.IsDefault.ShouldBeFalse();
        beta.Modules!.Select(module => module.Name).ShouldBe(new[] { "beta" });
        beta.Instructions.ShouldBe("Beta opening.\n\nBeta section.");
    }

    /// <summary>Sections follow module registration (dependency) order, not the order a host happened to list them in.</summary>
    [Fact]
    public void Should_Compose_Sections_In_Registration_Order()
    {
        var provider = CreateProvider(options =>
        {
            options.Modules.Add("beta");
            options.Modules.Add("alpha");
            options.Endpoints.Add(new AbpMcpEndpointOptions("/gamma-mcp") { Modules = { "gamma" } });
        }, extraModule: "gamma");

        provider.DefaultEndpoint.Instructions.ShouldBe("Opening.\n\nAlpha section.\n\nBeta section.");
    }

    [Fact]
    public void Should_Inherit_The_Servers_Body_Limit_Unless_Set()
    {
        var provider = CreateProvider(options =>
        {
            options.MaxRequestBodySize = 50 * 1024 * 1024;
            options.Modules.Add("alpha");
            options.Endpoints.Add(new AbpMcpEndpointOptions("/beta-mcp") { Modules = { "beta" } });
            options.Endpoints.Add(new AbpMcpEndpointOptions("/gamma-mcp") { Modules = { "gamma" }, MaxRequestBodySize = 1024 });
        }, extraModule: "gamma");

        provider.Endpoints.Select(endpoint => endpoint.MaxRequestBodySize).ShouldBe(new long?[] { 50 * 1024 * 1024, 50 * 1024 * 1024, 1024 });
    }

    [Fact]
    public void Should_Keep_Each_Endpoints_Conventions_Apart()
    {
        var provider = CreateProvider(options =>
        {
            options.Modules.Add("alpha");
            options.EndpointConventions.Add(_ => { });
            options.Endpoints.Add(new AbpMcpEndpointOptions("/beta-mcp") { Modules = { "beta" } });
        });

        provider.DefaultEndpoint.EndpointConventions.Count.ShouldBe(1);
        provider.Endpoints[1].EndpointConventions.ShouldBeEmpty();
    }

    /// <summary>
    /// A module installed later must not land on the default endpoint - which may be the anonymous one - without
    /// anyone deciding it.
    /// </summary>
    [Fact]
    public void Should_Refuse_A_Module_No_Endpoint_Serves()
    {
        var exception = Should.Throw<AbpException>(() => CreateProvider(options =>
            options.Endpoints.Add(new AbpMcpEndpointOptions("/beta-mcp") { Modules = { "beta" } })).Endpoints);

        exception.Message.ShouldContain("'alpha'");
        exception.Message.ShouldContain("served by no endpoint");
    }

    [Fact]
    public void Should_Refuse_A_Partial_Assignment_Without_Further_Endpoints()
    {
        var exception = Should.Throw<AbpException>(() => CreateProvider(options => options.Modules.Add("alpha")).Endpoints);

        exception.Message.ShouldContain("'beta'");
    }

    [Fact]
    public void Should_Refuse_A_Module_Served_By_Two_Endpoints()
    {
        var exception = Should.Throw<AbpException>(() => CreateProvider(options =>
        {
            options.Modules.Add("alpha");
            options.Modules.Add("beta");
            options.Endpoints.Add(new AbpMcpEndpointOptions("/beta-mcp") { Modules = { "beta" } });
        }).Endpoints);

        exception.Message.ShouldContain("'beta' is named by both the '/mcp' and the '/beta-mcp' endpoint");
    }

    [Fact]
    public void Should_Refuse_A_Module_Nobody_Registered()
    {
        var exception = Should.Throw<AbpException>(() => CreateProvider(options =>
        {
            options.Modules.Add("alpha");
            options.Endpoints.Add(new AbpMcpEndpointOptions("/beta-mcp") { Modules = { "beta", "betta" } });
        }).Endpoints);

        exception.Message.ShouldContain("'betta', which no module registered");
        exception.Message.ShouldContain("'alpha', 'beta'");
    }

    [Fact]
    public void Should_Refuse_An_Endpoint_That_Names_No_Module()
    {
        var exception = Should.Throw<AbpException>(() => CreateProvider(options =>
        {
            options.Modules.Add("alpha");
            options.Modules.Add("beta");
            options.Endpoints.Add(new AbpMcpEndpointOptions("/empty-mcp"));
        }).Endpoints);

        exception.Message.ShouldContain("'/empty-mcp' names no module");
    }

    [Theory]
    [InlineData("/mcp")]
    [InlineData("mcp/")]
    [InlineData("/MCP")]
    public void Should_Refuse_Two_Endpoints_At_One_Route(string route)
    {
        var exception = Should.Throw<AbpException>(() => CreateProvider(options =>
        {
            options.Modules.Add("alpha");
            options.Endpoints.Add(new AbpMcpEndpointOptions(route) { Modules = { "beta" } });
        }).Endpoints);

        exception.Message.ShouldContain($"Two MCP endpoints are mapped at '{route}'");
    }

    [Fact]
    public void Should_Report_Every_Problem_At_Once()
    {
        var exception = Should.Throw<AbpException>(() => CreateProvider(options =>
        {
            options.Endpoints.Add(new AbpMcpEndpointOptions("/x-mcp") { Modules = { "nope" } });
            options.Endpoints.Add(new AbpMcpEndpointOptions("/x-mcp") { Modules = { "alpha" } });
        }).Endpoints);

        exception.Message.ShouldContain("'nope'");
        exception.Message.ShouldContain("Two MCP endpoints");
        exception.Message.ShouldContain("'beta'");
    }

    [Theory]
    [InlineData("/beta-mcp")]
    [InlineData("beta-mcp")]
    [InlineData("/BETA-MCP/")]
    public void Should_Find_An_Endpoint_By_Its_Path(string path)
    {
        var provider = CreateProvider(options =>
        {
            options.Modules.Add("alpha");
            options.Endpoints.Add(new AbpMcpEndpointOptions("/beta-mcp") { Modules = { "beta" } });
        });

        provider.FindByPath(path)!.RoutePattern.ShouldBe("/beta-mcp");
        provider.FindByPath("/gamma-mcp").ShouldBeNull();
    }

    /// <summary>A session could be resumed at another endpoint than the one that created it - with its tools.</summary>
    [Fact]
    public void Should_Refuse_A_Split_Server_With_Sessions()
    {
        var provider = CreateProvider(options =>
        {
            options.Modules.Add("alpha");
            options.Endpoints.Add(new AbpMcpEndpointOptions("/beta-mcp") { Modules = { "beta" } });
        });

        Should.NotThrow(() => provider.Validate(new HttpServerTransportOptions()));
        var exception = Should.Throw<AbpException>(() => provider.Validate(new HttpServerTransportOptions
        {
            SessionMode = HttpServerSessionMode.StatefulForInitializeClients
        }));
        exception.Message.ShouldContain("stateless");
    }

    [Fact]
    public void Should_Allow_Sessions_On_A_Server_That_Is_Not_Split()
    {
        Should.NotThrow(() => CreateProvider(_ => { }).Validate(new HttpServerTransportOptions
        {
            SessionMode = HttpServerSessionMode.StatefulForInitializeClients
        }));
    }

    [Theory]
    [InlineData("https://api.example.com/mcp", "https://api.example.com/beta-mcp")]
    [InlineData("https://api.example.com/mcp/", "https://api.example.com/beta-mcp")]
    [InlineData("https://api.example.com", "https://api.example.com/beta-mcp")]
    [InlineData("https://api.example.com/", "https://api.example.com/beta-mcp")]
    [InlineData("https://example.com/app/mcp", "https://example.com/app/beta-mcp")]
    public void Should_Name_The_Endpoint_In_Its_Resource_Metadata(string configured, string expected)
    {
        var provider = CreateProvider(options =>
        {
            options.Modules.Add("alpha");
            options.Endpoints.Add(new AbpMcpEndpointOptions("/beta-mcp") { Modules = { "beta" } });
        });

        AbpMcpEndpointResourceMetadata.Create(Metadata(configured), provider.Endpoints[1], provider.DefaultEndpoint)
            .Resource.ShouldBe(expected);
        AbpMcpEndpointResourceMetadata.Create(Metadata(configured), provider.DefaultEndpoint, provider.DefaultEndpoint)
            .Resource.ShouldBe(configured);
    }

    [Fact]
    public void Should_Change_Only_The_Endpoints_Copy_Of_The_Metadata()
    {
        var provider = CreateProvider(options =>
        {
            options.Modules.Add("alpha");
            options.Endpoints.Add(new AbpMcpEndpointOptions("/beta-mcp")
            {
                Modules = { "beta" },
                ConfigureResourceMetadata = metadata =>
                {
                    metadata.ScopesSupported.Add("Beta");
                    metadata.ResourceName = "Beta";
                }
            });
        });
        var configured = Metadata("https://api.example.com/mcp");
        configured.ScopesSupported.Add("Alpha");

        var beta = AbpMcpEndpointResourceMetadata.Create(configured, provider.Endpoints[1], provider.DefaultEndpoint);

        beta.ScopesSupported.ShouldBe(new[] { "Alpha", "Beta" });
        beta.ResourceName.ShouldBe("Beta");
        beta.AuthorizationServers.ShouldBe(new[] { "https://id.example.com/" });
        configured.ScopesSupported.ShouldBe(new[] { "Alpha" });
        configured.ResourceName.ShouldBeNull();
    }

    private static ProtectedResourceMetadata Metadata(string resource)
    {
        var metadata = new ProtectedResourceMetadata { Resource = resource };
        metadata.AuthorizationServers.Add("https://id.example.com/");
        return metadata;
    }

    private static AbpMcpEndpointProvider CreateProvider(Action<AbpMcpServerOptions> configure, string? extraModule = null)
    {
        var registry = new AbpMcpModuleRegistry();
        registry.GetOrAdd("alpha").AddInstructions("Alpha section.");
        registry.GetOrAdd("beta").AddInstructions("Beta section.");
        if (extraModule != null)
        {
            registry.GetOrAdd(extraModule);
        }

        var options = new AbpMcpServerOptions { Instructions = "Opening." };
        configure(options);
        return new AbpMcpEndpointProvider(Options.Create(options), registry);
    }
}
