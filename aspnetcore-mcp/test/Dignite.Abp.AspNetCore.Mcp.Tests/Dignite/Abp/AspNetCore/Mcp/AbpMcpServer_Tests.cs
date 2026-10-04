using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.AspNetCore.Mcp.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Volo.Abp.AspNetCore.TestBase;
using Xunit;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// The server end to end: a real MCP client over HTTP, through the full ABP pipeline - authentication,
/// a dynamic-claims stand-in, authorization, the mapped endpoint.
/// </summary>
public class AbpMcpServer_Tests : AbpWebApplicationFactoryIntegratedTest<Program>
{
    [Fact]
    public async Task Should_Challenge_An_Unauthenticated_Request_With_Resource_Metadata()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await Client.SendAsync(request);

        // A 401 with a discovery pointer - not the host's default challenge, and not a redirect.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldContain("resource_metadata=");
    }

    [Fact]
    public async Task Should_Serve_The_Protected_Resource_Metadata_Document()
    {
        using var response = await Client.GetAsync("/.well-known/oauth-protected-resource/mcp");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("http://localhost");
    }

    [Fact]
    public async Task Should_Hide_A_Tool_The_User_Is_Not_Granted()
    {
        await using var client = await ConnectAsync();

        var toolNames = (await client.ListToolsAsync()).Select(tool => tool.Name).ToList();

        toolNames.ShouldContain("test_echo");
        toolNames.ShouldNotContain("test_secret");
    }

    [Fact]
    public async Task Should_Show_A_Tool_Granted_By_The_Token()
    {
        await using var client = await ConnectAsync(permissions: TestPermissionDefinitionProvider.Secret);

        (await client.ListToolsAsync()).Select(tool => tool.Name).ShouldContain("test_secret");
        var result = await client.CallToolAsync("test_secret");
        result.IsError.ShouldNotBe(true);
    }

    /// <summary>
    /// The regression this module was partly written to prevent: the endpoint must authorize against the
    /// principal the pipeline already built - including claims added after authentication, as ABP's
    /// dynamic claims are - rather than re-authenticating through a named scheme and discarding them.
    /// </summary>
    [Fact]
    public async Task Should_Authorize_Against_Claims_Added_After_Authentication()
    {
        await using var client = await ConnectAsync(dynamicPermission: TestPermissionDefinitionProvider.Secret);

        (await client.ListToolsAsync()).Select(tool => tool.Name).ShouldContain("test_secret");
    }

    [Fact]
    public async Task Should_Return_A_Structured_Validation_Error()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync("test_fail_validation");

        result.IsError.ShouldBe(true);
        var error = GetError(result);
        error.GetProperty("kind").GetString().ShouldBe("validation");
        error.GetProperty("validationErrors")[0].GetProperty("fields")[0].GetString().ShouldBe("slug");
    }

    [Fact]
    public async Task Should_Let_An_Exception_Declare_Its_Own_Error_Kind()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync("test_fail_not_found");

        result.IsError.ShouldBe(true);
        var error = GetError(result);
        error.GetProperty("kind").GetString().ShouldBe("notFound");
        error.GetProperty("code").GetString().ShouldBe("Test:001");
        error.GetProperty("message").GetString()!.ShouldContain("widget");
    }

    [Fact]
    public async Task Should_Resolve_The_Tool_Class_From_The_Abp_Container()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync("test_greet");

        result.Content.OfType<TextContentBlock>().Single().Text.ShouldBe("replaced");
    }

    [Fact]
    public async Task Should_Report_Server_Info_And_Compose_The_Instructions()
    {
        await using var client = await ConnectAsync();

        client.ServerInfo.Name.ShouldBe(AbpAspNetCoreMcpTestModule.ServerName);
        client.ServerInfo.Version.ShouldBe("9.9.9");
        client.ServerInstructions.ShouldBe(
            AbpAspNetCoreMcpTestModule.HostInstructions + "\n\n" + AbpAspNetCoreMcpTestModule.ModuleInstructions);
    }

    [Fact]
    public async Task Should_Read_A_Module_Resource()
    {
        await using var client = await ConnectAsync();

        var result = await client.ReadResourceAsync("test://info");

        result.Contents.OfType<TextResourceContents>().Single().Text.ShouldBe("info");
    }

    [Fact]
    public async Task Should_List_Static_And_Contributed_Resources_Together()
    {
        await using var client = await ConnectAsync();

        var uris = (await client.ListResourcesAsync()).Select(resource => resource.Uri).ToList();

        uris.ShouldContain("test://info");
        uris.ShouldContain("test://dynamic/1");
    }

    [Fact]
    public async Task Should_Pass_An_Mcp_Exception_Message_Through()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync("test_fail_mcp");

        result.IsError.ShouldBe(true);
        GetError(result).GetProperty("message").GetString().ShouldBe("Written for the caller.");
    }

    /// <summary>
    /// Protocol-level failures stay protocol-level, as the SDK's own pipeline keeps them: the error
    /// filter must not turn them into a tool outcome.
    /// </summary>
    [Fact]
    public async Task Should_Leave_Protocol_Errors_As_Protocol_Errors()
    {
        await using var client = await ConnectAsync();

        await Should.ThrowAsync<McpProtocolException>(async () => await client.CallToolAsync("test_fail_protocol"));
        await Should.ThrowAsync<McpProtocolException>(async () => await client.CallToolAsync("test_no_such_tool"));
    }

    /// <summary>
    /// The body limit must be endpoint metadata - applied by routing before the SDK reads the body - not a
    /// check inside a tool, which only runs once the whole request is already buffered.
    /// </summary>
    [Fact]
    public void Should_Limit_The_Request_Body_Before_It_Is_Read()
    {
        var endpoint = GetRequiredService<EndpointDataSource>().Endpoints
            .Single(candidate => candidate.Metadata.GetMetadata<AbpMcpEndpointMetadata>() != null);

        endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>()!.MaxRequestBodySize.ShouldBe(4 * 1024 * 1024);
    }

    private async Task<McpClient> ConnectAsync(string? permissions = null, string? dynamicPermission = null)
    {
        var headers = new Dictionary<string, string>
        {
            [TestAuthenticationHandler.UserHeader] = Guid.NewGuid().ToString()
        };
        if (permissions != null)
        {
            headers[TestAuthenticationHandler.PermissionsHeader] = permissions;
        }
        if (dynamicPermission != null)
        {
            headers[TestAuthenticationHandler.DynamicPermissionHeader] = dynamicPermission;
        }

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(Client.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = headers
            },
            Client);

        return await McpClient.CreateAsync(transport);
    }

    private static JsonElement GetError(CallToolResult result)
    {
        result.StructuredContent.ShouldNotBeNull();
        return result.StructuredContent!.Value.GetProperty("error");
    }
}
