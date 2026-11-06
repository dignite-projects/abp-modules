using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;

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
    /// order; see <see cref="AbpMcpModuleBuilder.AddInstructions"/>. With further <see cref="Endpoints"/>, it
    /// opens the default endpoint's instructions only, and each endpoint has an opening of its own.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// The largest request body the endpoint accepts, in bytes - enforced by routing before the body is
    /// read, so an oversized request is refused with <c>413</c> instead of being buffered and parsed first.
    /// Defaults to 4 MB, ample for JSON-RPC tool arguments. A module whose tools take larger arguments
    /// raises it for itself (a module that accepts base64 uploads would). <c>null</c> leaves the
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
    /// The JSON options every tool, resource and prompt registered through <see cref="AbpMcpModuleBuilder"/>
    /// is built with: a tool and a prompt read their arguments with them, and a tool writes its result with
    /// them - the text a model reads when a tool returns a DTO. (A prompt returns text or content blocks as they
    /// are. A resource is given them too, but the SDK writes no DTO for it and converts its URI variables itself,
    /// so they change nothing a resource does yet.)
    /// <para>
    /// Defaults to a copy of the SDK's <see cref="McpJsonUtilities.DefaultOptions"/> - so its type resolver,
    /// web naming, string enums and null handling are unchanged - with one difference:
    /// <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> as the encoder. The SDK's own encoder escapes
    /// every non-ASCII character and <c>&lt;</c>, <c>&gt;</c>, <c>&amp;</c>, <c>'</c>, <c>"</c>, <c>+</c> and the
    /// backtick as <c>\uXXXX</c>, so Chinese text or an HTML template reaches the model several times longer than
    /// it is. The relaxed encoder writes them as they are. It still escapes <c>"</c> and <c>\</c> (as JSON
    /// requires), control characters, whitespace other than the ASCII space (U+00A0 and the ideographic space
    /// U+3000 among them), U+2028, U+2029 and U+FEFF, private-use and unassigned code points, and every
    /// character outside the Basic Multilingual Plane (emoji, rare Chinese characters). An encoder made from
    /// Unicode ranges does not write those either; that takes an encoder of your own, set as <c>Encoder</c>
    /// on these options.
    /// The result is JSON in a text block, never HTML, and the JSON-RPC message that carries it is still written
    /// by the SDK's own options, so the HTTP response is escaped as before.
    /// </para>
    /// <para>
    /// Change it by adding to the default (<c>options.SerializerOptions.Converters.Add(...)</c>) rather than
    /// replacing it: an instance built with <c>new JsonSerializerOptions()</c> has none of the SDK's contracts and
    /// would serialize a DTO differently. It is shared by every primitive and made read-only by the first
    /// one built, so set it in <c>Configure&lt;AbpMcpServerOptions&gt;</c> and not while the application runs.
    /// </para>
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; set; } = CreateDefaultSerializerOptions();

    private static JsonSerializerOptions CreateDefaultSerializerOptions()
    {
        return new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    /// <summary>
    /// Extra conventions applied to the mapped endpoint, after <c>RequireAuthorization()</c> - rate
    /// limiting, CORS, a stricter policy. For example
    /// <c>options.EndpointConventions.Add(endpoint => endpoint.RequireRateLimiting("mcp"))</c>.
    /// With further <see cref="Endpoints"/>, these are the default endpoint's (<see cref="RoutePattern"/>) only.
    /// </summary>
    public List<Action<IEndpointConventionBuilder>> EndpointConventions { get; } = new();

    /// <summary>
    /// The MCP modules (namespaces) served at <see cref="RoutePattern"/>, by the name each registered with
    /// <c>AddAbpMcpModule</c>.
    /// <para>
    /// Empty, and with no <see cref="Endpoints"/>, the endpoint serves every module - one endpoint, as before
    /// endpoints could be split. As soon as this or any endpoint names a module, <b>every</b> module must be
    /// named by exactly one endpoint, and the application refuses to start otherwise: a module installed later
    /// would otherwise land on whichever endpoint happens to be the default, which may be an anonymous one.
    /// </para>
    /// </summary>
    public List<string> Modules { get; } = new();

    /// <summary>
    /// Further endpoints of the same server, each serving the modules it names; see <see cref="AbpMcpEndpointOptions"/>.
    /// The endpoint configured by this class's own <see cref="RoutePattern"/>, <see cref="Instructions"/>,
    /// <see cref="MaxRequestBodySize"/>, <see cref="EndpointConventions"/> and <see cref="Modules"/> is always mapped too.
    /// </summary>
    public List<AbpMcpEndpointOptions> Endpoints { get; } = new();
}
