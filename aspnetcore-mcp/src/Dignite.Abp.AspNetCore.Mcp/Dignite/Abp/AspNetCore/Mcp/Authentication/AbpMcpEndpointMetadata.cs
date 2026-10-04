namespace Dignite.Abp.AspNetCore.Mcp.Authentication;

/// <summary>
/// Endpoint metadata marking the MCP endpoint, so <see cref="AbpMcpAuthorizationMiddlewareResultHandler"/>
/// can answer its unauthenticated requests with the MCP challenge and leave every other endpoint alone.
/// </summary>
public sealed class AbpMcpEndpointMetadata
{
    public static readonly AbpMcpEndpointMetadata Instance = new();

    private AbpMcpEndpointMetadata()
    {
    }
}
