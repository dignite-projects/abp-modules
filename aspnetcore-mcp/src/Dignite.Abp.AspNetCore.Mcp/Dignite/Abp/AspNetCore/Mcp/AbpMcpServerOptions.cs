using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// The host's settings for the application's MCP server - everything that exists once per server.
/// <para>
/// Set with an ordinary <c>Configure&lt;AbpMcpServerOptions&gt;</c>: every value here is read when the
/// server or its endpoint is built, never while services are still being registered. What modules
/// contribute (tools, resources, prompts, their instruction sections) is not here - that is
/// <see cref="AbpMcpServiceCollectionExtensions.AddAbpMcpModule"/>.
/// </para>
/// </summary>
public class AbpMcpServerOptions
{
    /// <summary>
    /// Where the Streamable HTTP endpoint is mapped. Defaults to <c>/mcp</c>, the path clients assume
    /// when a user pastes a bare origin.
    /// </summary>
    public string RoutePattern { get; set; } = "/mcp";

    /// <summary>
    /// The server name reported in the MCP <c>initialize</c> handshake. Defaults to the ABP application
    /// name, then to the entry assembly's name.
    /// </summary>
    public string? ServerName { get; set; }

    /// <summary>The server version reported in the MCP <c>initialize</c> handshake.</summary>
    public string ServerVersion { get; set; } = "1.0.0";

    /// <summary>
    /// An optional opening section for the instructions handed to a client on connect - what this
    /// server is as a whole. Each contributing module's own section follows it, in module dependency
    /// order; see <see cref="AbpMcpModuleBuilder.AddInstructions"/>.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// The largest request body the endpoint accepts, in bytes - enforced by routing before the body is
    /// read, so an oversized request is refused with <c>413</c> instead of being buffered and parsed first.
    /// Defaults to 4 MB, ample for JSON-RPC tool arguments. A module whose tools take larger arguments
    /// raises it for itself (Dignite.FileExplorer.Mcp does, to fit its base64 uploads). <c>null</c> leaves the
    /// server's own limit (Kestrel: 30 MB) in place.
    /// </summary>
    public long? MaxRequestBodySize { get; set; } = 4 * 1024 * 1024;

    /// <summary>
    /// Whether tools, resources and prompts registered outside any MCP module - by a library that uses the
    /// SDK's own <c>WithTools&lt;T&gt;()</c> family - may start. Off by default: such a primitive belongs to no
    /// namespace, so nothing keeps the next module from colliding with it. Turning it on is the way to host a
    /// third-party MCP tool library that cannot be changed; duplicate names are still refused either way.
    /// </summary>
    public bool AllowUnownedPrimitives { get; set; }

    /// <summary>
    /// Extra conventions applied to the mapped endpoint, after <c>RequireAuthorization()</c> - rate
    /// limiting, CORS, a stricter policy. For example
    /// <c>options.EndpointConventions.Add(endpoint => endpoint.RequireRateLimiting("mcp"))</c>.
    /// </summary>
    public List<Action<IEndpointConventionBuilder>> EndpointConventions { get; } = new();
}
