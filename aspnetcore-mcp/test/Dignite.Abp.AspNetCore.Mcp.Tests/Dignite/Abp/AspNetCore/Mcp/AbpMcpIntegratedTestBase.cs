using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Volo.Abp.AspNetCore.TestBase;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>A real MCP client over HTTP against the test host, through the full ABP pipeline.</summary>
public abstract class AbpMcpIntegratedTestBase : AbpWebApplicationFactoryIntegratedTest<Program>
{
    protected async Task<McpClient> ConnectAsync(string? permissions = null, string? dynamicPermission = null)
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

    protected static JsonElement GetError(CallToolResult result)
    {
        result.StructuredContent.ShouldNotBeNull();
        return result.StructuredContent!.Value.GetProperty("error");
    }
}
