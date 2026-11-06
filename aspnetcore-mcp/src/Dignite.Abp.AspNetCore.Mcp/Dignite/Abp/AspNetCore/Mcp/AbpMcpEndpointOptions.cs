using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using ModelContextProtocol.Authentication;
using Volo.Abp;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// A further endpoint of the application's MCP server, added to <see cref="AbpMcpServerOptions.Endpoints"/>: its own
/// route, serving only the MCP modules (namespaces) it names, with its own instructions, endpoint conventions and
/// RFC 9728 resource metadata.
/// <para>
/// <b>Why a host splits its server.</b> The endpoints of one server are seen by different audiences. A public,
/// anonymous calculation endpoint and an administration endpoint that needs a signed-in user are the usual pair:
/// on one endpoint, every anonymous <c>tools/list</c> would carry the administration tools (their permissions are
/// checked, but some only when called) and the connect instructions would describe both. Each endpoint is the same
/// server - one set of registrations, one validation, one serializer - showing a different part of it.
/// </para>
/// </summary>
/// <example>
/// <code>
/// Configure&lt;AbpMcpServerOptions&gt;(options =>
/// {
///     options.Modules.Add("calc");                       // served at RoutePattern, /mcp
///     options.EndpointConventions.Add(e => e.AllowAnonymous());
///     options.Endpoints.Add(new AbpMcpEndpointOptions("/admin-mcp")
///     {
///         Modules = { "admin" },
///         Instructions = "Administration of My Application."
///     });
/// });
/// </code>
/// </example>
public class AbpMcpEndpointOptions
{
    public AbpMcpEndpointOptions(string routePattern)
    {
        Check.NotNullOrWhiteSpace(routePattern, nameof(routePattern));
        RoutePattern = routePattern;
    }

    /// <summary>Where this endpoint is mapped, for example <c>/admin-mcp</c>.</summary>
    public string RoutePattern { get; }

    /// <summary>
    /// The MCP modules this endpoint serves, by the name each registered with <c>AddAbpMcpModule</c> - which is
    /// both the tool and prompt name prefix and (by default) the resource URI scheme. At least one; a module may
    /// be served by one endpoint only.
    /// </summary>
    public List<string> Modules { get; } = new();

    /// <summary>
    /// The opening section of this endpoint's instructions, before the sections of the modules it serves.
    /// <see cref="AbpMcpServerOptions.Instructions"/> opens the default endpoint's only.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// The largest request body this endpoint accepts, in bytes. <c>null</c> (the default) takes
    /// <see cref="AbpMcpServerOptions.MaxRequestBodySize"/> - which is also what a module raises for its own large
    /// arguments, so a module keeps the room it asked for on whichever endpoint serves it.
    /// </summary>
    public long? MaxRequestBodySize { get; set; }

    /// <summary>
    /// Conventions applied to this endpoint after <c>RequireAuthorization()</c>. The server's own
    /// <see cref="AbpMcpServerOptions.EndpointConventions"/> are the default endpoint's and are not applied here:
    /// an anonymous, rate-limited public endpoint must not make the next endpoint anonymous too.
    /// </summary>
    public List<Action<IEndpointConventionBuilder>> EndpointConventions { get; } = new();

    /// <summary>
    /// Changes this endpoint's copy of the RFC 9728 protected resource metadata that
    /// <c>AddAbpMcpAuthenticationDiscovery</c> configured - typically <see cref="ProtectedResourceMetadata.ScopesSupported"/>
    /// or <see cref="ProtectedResourceMetadata.ResourceName"/>. The copy's
    /// <see cref="ProtectedResourceMetadata.Resource"/> already names this endpoint (see the hosting documentation).
    /// It is served at <c>/.well-known/oauth-protected-resource{RoutePattern}</c>, which is where this endpoint's
    /// <c>401</c> challenge points.
    /// </summary>
    public Action<ProtectedResourceMetadata>? ConfigureResourceMetadata { get; set; }
}
