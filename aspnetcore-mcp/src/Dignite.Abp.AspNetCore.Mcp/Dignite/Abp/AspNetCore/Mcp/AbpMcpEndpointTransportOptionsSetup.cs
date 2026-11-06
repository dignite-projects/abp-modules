using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Volo.Abp;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// Narrows the server to the endpoint a request reached, when the server is split across endpoints
/// (<see cref="AbpMcpServerOptions.Endpoints"/>): the tools, resources and prompts of other endpoints' modules are
/// taken out, and the instructions and the <c>resources/list</c> contributors are the endpoint's own.
/// <para>
/// <b>Why through <see cref="HttpServerTransportOptions.ConfigureSessionOptions"/>.</b> The stateless transport builds
/// a fresh <see cref="McpServerOptions"/> for every HTTP request and hands it, with the request's
/// <see cref="HttpContext"/>, to that callback before the server is created from it. Taking primitives out of the
/// request's own collections there - rather than filtering list results and refusing calls one by one - means every
/// operation agrees by construction: <c>tools/list</c>, <c>tools/call</c>, <c>resources/read</c>, resource templates,
/// <c>prompts/get</c> and completions all see the same, smaller server, and a call to another endpoint's tool is the
/// SDK's own "unknown tool". The SDK's authorization filters capture the collections when the server is created,
/// after this callback, so they filter the narrowed ones.
/// </para>
/// <para>
/// Installed only when the server is split; a server with one endpoint is left exactly as the SDK builds it. A
/// <c>ConfigureSessionOptions</c> the host set itself still runs, after this one, and sees the narrowed server.
/// </para>
/// </summary>
public class AbpMcpEndpointTransportOptionsSetup : IPostConfigureOptions<HttpServerTransportOptions>
{
    protected AbpMcpEndpointProvider EndpointProvider { get; }

    protected AbpMcpPrimitiveCache Cache { get; }

    protected AbpMcpModuleRegistry Registry { get; }

    public AbpMcpEndpointTransportOptionsSetup(
        AbpMcpEndpointProvider endpointProvider,
        AbpMcpPrimitiveCache cache,
        AbpMcpModuleRegistry registry)
    {
        EndpointProvider = endpointProvider;
        Cache = cache;
        Registry = registry;
    }

    public virtual void PostConfigure(string? name, HttpServerTransportOptions options)
    {
        if (!EndpointProvider.IsSplit)
        {
            return;
        }

        var hostCallback = options.ConfigureSessionOptions;
        options.ConfigureSessionOptions = async (httpContext, serverOptions, cancellationToken) =>
        {
            ConfigureForEndpoint(GetEndpoint(httpContext), serverOptions);

            if (hostCallback != null)
            {
                await hostCallback(httpContext, serverOptions, cancellationToken);
            }
        };
    }

    /// <summary>
    /// The endpoint the request was routed to. A request that reached the SDK's handler through a route this module
    /// did not map - a host's own <c>MapMcp</c> - is refused rather than served the whole server: which part of it
    /// that route should show is not known, and showing all of it is what splitting the server exists to prevent.
    /// </summary>
    protected virtual AbpMcpEndpoint GetEndpoint(HttpContext httpContext)
    {
        return httpContext.GetEndpoint()?.Metadata.GetMetadata<AbpMcpEndpoint>()
               ?? throw new AbpException(
                   $"The MCP request to '{httpContext.Request.Path}' did not come through an endpoint mapped by " +
                   $"{nameof(AbpAspNetCoreMcpModule)}. The server is split across endpoints, and a route mapped " +
                   "elsewhere is not one of them: add it to AbpMcpServerOptions.Endpoints instead.");
    }

    protected virtual void ConfigureForEndpoint(AbpMcpEndpoint endpoint, McpServerOptions options)
    {
        // An emptied collection becomes null, as the SDK leaves it when there is nothing to register, so the
        // endpoint does not advertise a capability it has nothing for.
        Narrow(options.ToolCollection, endpoint);
        if (options.ToolCollection?.IsEmpty == true)
        {
            options.ToolCollection = null;
        }

        Narrow(options.PromptCollection, endpoint);
        if (options.PromptCollection?.IsEmpty == true)
        {
            options.PromptCollection = null;
        }

        Narrow(options.ResourceCollection, endpoint);
        if (options.ResourceCollection?.IsEmpty == true)
        {
            options.ResourceCollection = null;
        }

        options.ServerInstructions = endpoint.Instructions;

        // AbpMcpServerOptionsSetup takes the resources/list slot for every module's contributors whenever any module
        // has one. Here it either runs this endpoint's contributors only, or is given back to the SDK's own listing of
        // the (already narrowed) static resources. A slot it never took - a host's own handler - is left alone.
        if (Registry.Modules.Any(module => module.ResourceListContributors.Count > 0))
        {
            options.Handlers.ListResourcesHandler = endpoint.Modules!.Any(module => module.ResourceListContributors.Count > 0)
                ? (request, cancellationToken) => ListResourcesAsync(request, endpoint, cancellationToken)
                : null;
        }
    }

    /// <summary>
    /// Takes out, in place, what <paramref name="endpoint"/> does not serve. The collection is this request's own -
    /// the SDK fills a new one for every <see cref="McpServerOptions"/> it builds - so nothing else sees the change.
    /// </summary>
    protected virtual void Narrow<TPrimitive>(McpServerPrimitiveCollection<TPrimitive>? collection, AbpMcpEndpoint endpoint)
        where TPrimitive : IMcpServerPrimitive
    {
        if (collection == null)
        {
            return;
        }

        foreach (var primitive in collection.ToList())
        {
            if (!endpoint.Serves(Cache.FindOwner(primitive)))
            {
                collection.Remove(primitive);
            }
        }
    }

    private static ValueTask<ListResourcesResult> ListResourcesAsync(
        RequestContext<ListResourcesRequestParams> request,
        AbpMcpEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var services = request.Services
            ?? throw new AbpException("An MCP resources/list request carries no service provider.");
        return new ValueTask<ListResourcesResult>(services
            .GetRequiredService<AbpMcpResourceListAggregator>()
            .ListAsync(services, endpoint.Modules!, cancellationToken));
    }
}
