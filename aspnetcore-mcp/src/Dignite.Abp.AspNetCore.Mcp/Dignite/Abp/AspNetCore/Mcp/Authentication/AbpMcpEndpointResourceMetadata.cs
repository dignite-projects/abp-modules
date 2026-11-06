using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;

namespace Dignite.Abp.AspNetCore.Mcp.Authentication;

/// <summary>
/// The RFC 9728 protected resource metadata of each endpoint of a split server (<see cref="AbpMcpServerOptions.Endpoints"/>).
/// <para>
/// <b>Where each endpoint's document lives.</b> Left at its default, the SDK serves every path under
/// <c>/.well-known/oauth-protected-resource</c> and points an endpoint's <c>401</c> at the one that mirrors the
/// endpoint's own path - <c>/.well-known/oauth-protected-resource/admin-mcp</c> for <c>/admin-mcp</c>, as RFC 9728
/// §3.1 places it. What it serves there is the one configured document. This picks, by that path, the endpoint the
/// document is about and serves that endpoint's copy instead; the default endpoint keeps the configured document as
/// it is. A host that sets <see cref="McpAuthenticationOptions.ResourceMetadataUri"/> has chosen one address for
/// every endpoint, and every endpoint gets the configured document there.
/// </para>
/// <para>
/// <b>An endpoint's copy names the endpoint.</b> RFC 9728 §3.3 has a client reject a document whose <c>resource</c>
/// is not the identifier it was asked about. A configured resource that ends with the default endpoint's route
/// (<c>https://api.example.com/mcp</c>) has that route replaced (<c>https://api.example.com/admin-mcp</c>); any
/// other (an origin) has the endpoint's route appended. The endpoint's
/// <see cref="AbpMcpEndpointOptions.ConfigureResourceMetadata"/> runs on the copy afterwards and may set it again.
/// </para>
/// </summary>
internal static class AbpMcpEndpointResourceMetadata
{
    private static readonly PathString ResourceMetadataPrefix = new("/.well-known/oauth-protected-resource");

    /// <summary>
    /// The <c>OnResourceMetadataRequest</c> event <c>AddAbpMcpAuthenticationDiscovery</c> installs: swaps in the
    /// endpoint's copy when the request is for an endpoint other than the default one.
    /// </summary>
    public static Task SelectAsync(ResourceMetadataRequestContext context)
    {
        if (context.ResourceMetadata == null ||
            !context.HttpContext.Request.Path.StartsWithSegments(ResourceMetadataPrefix, out var remaining))
        {
            return Task.CompletedTask;
        }

        var endpointProvider = context.HttpContext.RequestServices.GetService<AbpMcpEndpointProvider>();
        var endpoint = endpointProvider?.FindByPath(remaining.Value);
        if (endpointProvider != null && endpoint is { IsDefault: false })
        {
            context.ResourceMetadata = Create(context.ResourceMetadata, endpoint, endpointProvider.DefaultEndpoint);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The metadata of the endpoint an MCP request reached - for the scope of its challenge. The configured document
    /// itself for the default endpoint, or when the request is not to an endpoint of this module.
    /// </summary>
    public static ProtectedResourceMetadata? ForRequest(HttpContext context, ProtectedResourceMetadata? configured)
    {
        var endpoint = context.GetEndpoint()?.Metadata.GetMetadata<AbpMcpEndpoint>();
        var endpointProvider = context.RequestServices.GetService<AbpMcpEndpointProvider>();
        return configured == null || endpoint is not { IsDefault: false } || endpointProvider == null
            ? configured
            : Create(configured, endpoint, endpointProvider.DefaultEndpoint);
    }

    public static ProtectedResourceMetadata Create(
        ProtectedResourceMetadata configured,
        AbpMcpEndpoint endpoint,
        AbpMcpEndpoint defaultEndpoint)
    {
        var metadata = configured.Clone();
        if (endpoint.IsDefault)
        {
            return metadata;
        }

        if (configured.Resource != null)
        {
            metadata.Resource = RebaseResource(configured.Resource, defaultEndpoint.RoutePattern, endpoint.RoutePattern);
        }

        endpoint.ConfigureResourceMetadata?.Invoke(metadata);
        return metadata;
    }

    private static string RebaseResource(string resource, string defaultRoute, string route)
    {
        var root = resource.TrimEnd('/');
        var defaultPath = "/" + defaultRoute.Trim().Trim('/');
        if (defaultPath != "/" && root.EndsWith(defaultPath, StringComparison.OrdinalIgnoreCase))
        {
            root = root[..^defaultPath.Length];
        }

        return root + "/" + route.Trim().Trim('/');
    }
}
