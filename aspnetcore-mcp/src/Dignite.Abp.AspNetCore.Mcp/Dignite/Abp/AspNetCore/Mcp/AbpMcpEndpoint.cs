using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using ModelContextProtocol.Authentication;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// One mapped endpoint of the MCP server, as <see cref="AbpMcpEndpointProvider"/> resolved it from
/// <see cref="AbpMcpServerOptions"/>: the modules it serves, its composed instructions and its conventions.
/// <para>
/// It is also endpoint metadata on every route the SDK maps for it, which is how a request - and the authorization
/// challenge of a request - knows which endpoint it reached:
/// <c>httpContext.GetEndpoint()?.Metadata.GetMetadata&lt;AbpMcpEndpoint&gt;()</c>.
/// </para>
/// </summary>
public class AbpMcpEndpoint
{
    public AbpMcpEndpoint(
        string routePattern,
        bool isDefault,
        IReadOnlyList<AbpMcpModule>? modules,
        string? instructions,
        long? maxRequestBodySize,
        IReadOnlyList<Action<IEndpointConventionBuilder>> endpointConventions,
        Action<ProtectedResourceMetadata>? configureResourceMetadata = null)
    {
        RoutePattern = routePattern;
        IsDefault = isDefault;
        Modules = modules;
        Instructions = instructions;
        MaxRequestBodySize = maxRequestBodySize;
        EndpointConventions = endpointConventions;
        ConfigureResourceMetadata = configureResourceMetadata;
    }

    public string RoutePattern { get; }

    /// <summary>
    /// The endpoint at <see cref="AbpMcpServerOptions.RoutePattern"/>, configured by <see cref="AbpMcpServerOptions"/>
    /// itself. It is the one that serves primitives registered outside any module
    /// (<see cref="AbpMcpServerOptions.AllowUnownedPrimitives"/>).
    /// </summary>
    public bool IsDefault { get; }

    /// <summary>
    /// The modules this endpoint serves, in registration (module dependency) order; <c>null</c> when the server has
    /// not been split and this one endpoint serves every module.
    /// </summary>
    public IReadOnlyList<AbpMcpModule>? Modules { get; }

    /// <summary>The instructions handed to a client of this endpoint: its opening, then its modules' sections.</summary>
    public string? Instructions { get; }

    public long? MaxRequestBodySize { get; }

    public IReadOnlyList<Action<IEndpointConventionBuilder>> EndpointConventions { get; }

    /// <summary>See <see cref="AbpMcpEndpointOptions.ConfigureResourceMetadata"/>. Always <c>null</c> on the default endpoint.</summary>
    public Action<ProtectedResourceMetadata>? ConfigureResourceMetadata { get; }

    /// <summary>Whether this endpoint serves every module - the server has one endpoint and has not been split.</summary>
    public bool ServesEveryModule => Modules == null;

    public virtual bool Serves(AbpMcpModule module)
    {
        return Modules == null || Modules.Contains(module);
    }

    /// <summary>
    /// Whether a primitive with this owner is served here. A primitive registered outside any module
    /// (<paramref name="owner"/> is <c>null</c>) belongs to no namespace and is served by the default endpoint only.
    /// </summary>
    public virtual bool Serves(AbpMcpPrimitiveRegistration? owner)
    {
        return owner == null ? IsDefault : Serves(owner.Module);
    }

    public override string ToString()
    {
        return RoutePattern;
    }
}
