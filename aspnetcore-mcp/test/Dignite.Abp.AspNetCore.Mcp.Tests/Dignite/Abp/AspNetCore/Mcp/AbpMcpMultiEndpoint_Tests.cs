using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// One server split across two endpoints, set up the way a host with a public calculation endpoint and a signed-in
/// administration endpoint would: the "test" module at an anonymous <c>/mcp</c>, the "other" module at
/// <c>/other-mcp</c>, which keeps the default policy. Each endpoint must show only its own module - in every list,
/// every call, its instructions and its discovery metadata - and carry only its own conventions.
/// </summary>
public class AbpMcpMultiEndpoint_Tests : AbpMcpIntegratedTestBase
{
    public const string OtherRoute = "/other-mcp";
    public const string OtherHostInstructions = "Other endpoint section.";
    public const string OtherModuleInstructions = "Other module section.";
    public const string OtherScope = "OtherScope";

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddAbpMcpModule("other", mcp => mcp
            .AddTools<OtherTools>()
            .AddResources<OtherResources>()
            .AddPrompts<OtherPrompts>()
            .AddResourceListContributor<OtherResourceListContributor>()
            .AddInstructions(OtherModuleInstructions));

        services.Configure<AbpMcpServerOptions>(options =>
        {
            options.Modules.Add("test");
            options.EndpointConventions.Add(endpoint => endpoint.AllowAnonymous());

            options.Endpoints.Add(new AbpMcpEndpointOptions(OtherRoute)
            {
                Modules = { "other" },
                Instructions = OtherHostInstructions,
                MaxRequestBodySize = 1024 * 1024,
                EndpointConventions = { endpoint => endpoint.WithMetadata(new OtherEndpointMarker()) },
                ConfigureResourceMetadata = metadata => metadata.ScopesSupported.Add(OtherScope)
            });
        });
    }

    [Fact]
    public async Task Should_List_Only_The_Endpoints_Own_Tools()
    {
        await using var defaultClient = await ConnectAsync(anonymous: true);
        await using var otherClient = await ConnectAsync(path: OtherRoute);

        var defaultTools = (await defaultClient.ListToolsAsync()).Select(tool => tool.Name).ToList();
        var otherTools = (await otherClient.ListToolsAsync()).Select(tool => tool.Name).ToList();

        defaultTools.ShouldContain("test_echo");
        defaultTools.ShouldNotContain(name => name.StartsWith("other_"));
        // Still filtered by permission, the narrowed list as much as the whole one.
        defaultTools.ShouldNotContain("test_secret");

        otherTools.ShouldBe(new[] { "other_ping" });
    }

    [Fact]
    public async Task Should_Refuse_A_Call_To_Another_Endpoints_Tool()
    {
        await using var defaultClient = await ConnectAsync(anonymous: true);
        await using var otherClient = await ConnectAsync(path: OtherRoute);

        await Should.ThrowAsync<McpProtocolException>(async () => await defaultClient.CallToolAsync("other_ping"));
        await Should.ThrowAsync<McpProtocolException>(async () => await otherClient.CallToolAsync("test_echo",
            new System.Collections.Generic.Dictionary<string, object?> { ["text"] = "x" }));

        (await otherClient.CallToolAsync("other_ping")).Content.OfType<TextContentBlock>().Single().Text.ShouldBe("pong");
    }

    [Fact]
    public async Task Should_List_And_Read_Only_The_Endpoints_Own_Resources()
    {
        await using var defaultClient = await ConnectAsync(anonymous: true);
        await using var otherClient = await ConnectAsync(path: OtherRoute);

        var defaultUris = (await defaultClient.ListResourcesAsync()).Select(resource => resource.Uri).ToList();
        var otherUris = (await otherClient.ListResourcesAsync()).Select(resource => resource.Uri).ToList();

        // The static resources and the contributed ones, each endpoint's own only.
        defaultUris.ShouldBe(new[] { "test://dynamic/1", "test://info" }, ignoreOrder: true);
        otherUris.ShouldBe(new[] { "other://dynamic/1", "other://info" }, ignoreOrder: true);

        (await otherClient.ReadResourceAsync("other://info")).Contents.OfType<TextResourceContents>().Single().Text
            .ShouldBe("other info");
        await Should.ThrowAsync<McpException>(async () => await defaultClient.ReadResourceAsync("other://info"));
        await Should.ThrowAsync<McpException>(async () => await otherClient.ReadResourceAsync("test://info"));
    }

    [Fact]
    public async Task Should_List_And_Get_Only_The_Endpoints_Own_Prompts()
    {
        await using var defaultClient = await ConnectAsync(anonymous: true);
        await using var otherClient = await ConnectAsync(path: OtherRoute);

        (await defaultClient.ListPromptsAsync()).Select(prompt => prompt.Name).ShouldBe(new[] { "test_describe_mode" });
        (await otherClient.ListPromptsAsync()).Select(prompt => prompt.Name).ShouldBe(new[] { "other_hello" });

        await Should.ThrowAsync<McpException>(async () => await defaultClient.GetPromptAsync("other_hello"));
        (await otherClient.GetPromptAsync("other_hello")).Messages.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Should_Compose_Each_Endpoints_Own_Instructions()
    {
        await using var defaultClient = await ConnectAsync(anonymous: true);
        await using var otherClient = await ConnectAsync(path: OtherRoute);

        defaultClient.ServerInstructions.ShouldBe(
            AbpAspNetCoreMcpTestModule.HostInstructions + "\n\n" + AbpAspNetCoreMcpTestModule.ModuleInstructions);
        otherClient.ServerInstructions.ShouldBe(OtherHostInstructions + "\n\n" + OtherModuleInstructions);

        // One server: the name and version are the same at every endpoint.
        otherClient.ServerInfo.Name.ShouldBe(AbpAspNetCoreMcpTestModule.ServerName);
    }

    /// <summary>
    /// The default endpoint's conventions made it anonymous; the other endpoint, which has conventions of its own,
    /// still needs a user - and its 401 points at its own metadata document and names its own scope.
    /// </summary>
    [Fact]
    public async Task Should_Challenge_At_The_Endpoint_That_Needs_A_User_With_Its_Own_Metadata()
    {
        using var defaultResponse = await PostToolsListAsync("/mcp");
        defaultResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var otherResponse = await PostToolsListAsync(OtherRoute);
        otherResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var challenge = otherResponse.Headers.WwwAuthenticate.ToString();
        challenge.ShouldContain("resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/other-mcp\"");
        challenge.ShouldEndWith($"scope=\"{OtherScope}\"");
    }

    [Fact]
    public async Task Should_Serve_Each_Endpoints_Own_Resource_Metadata()
    {
        var other = await GetJsonAsync("/.well-known/oauth-protected-resource/other-mcp");
        other.GetProperty("resource").GetString().ShouldBe("http://localhost/other-mcp");
        other.GetProperty("scopes_supported").EnumerateArray().Select(scope => scope.GetString()).ShouldBe(new[] { OtherScope });
        other.GetProperty("authorization_servers")[0].GetString().ShouldBe("http://localhost");

        // The default endpoint's document is the configured one, unchanged - the other endpoint's scope did not leak.
        var @default = await GetJsonAsync("/.well-known/oauth-protected-resource/mcp");
        @default.GetProperty("resource").GetString().ShouldBe("http://localhost");
        if (@default.TryGetProperty("scopes_supported", out var scopes))
        {
            scopes.EnumerateArray().Select(scope => scope.GetString()).ShouldNotContain(OtherScope);
        }
    }

    [Fact]
    public void Should_Apply_Each_Endpoints_Own_Conventions()
    {
        var endpoints = GetRequiredService<EndpointDataSource>().Endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<AbpMcpEndpoint>() != null)
            .ToList();
        var defaultEndpoints = endpoints.Where(endpoint => endpoint.Metadata.GetMetadata<AbpMcpEndpoint>()!.IsDefault).ToList();
        var otherEndpoints = endpoints.Where(endpoint => !endpoint.Metadata.GetMetadata<AbpMcpEndpoint>()!.IsDefault).ToList();

        defaultEndpoints.ShouldNotBeEmpty();
        otherEndpoints.ShouldNotBeEmpty();

        defaultEndpoints.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null);
        defaultEndpoints.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<OtherEndpointMarker>() == null);
        defaultEndpoints.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>()!.MaxRequestBodySize == 4 * 1024 * 1024);

        otherEndpoints.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() == null);
        otherEndpoints.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<OtherEndpointMarker>() != null);
        otherEndpoints.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>()!.MaxRequestBodySize == 1024 * 1024);
    }

    private async Task<HttpResponseMessage> PostToolsListAsync(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await Client.SendAsync(request);
    }

    private async Task<JsonElement> GetJsonAsync(string path)
    {
        using var response = await Client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
}
