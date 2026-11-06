using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// Resolves the endpoints the MCP server is mapped at - <see cref="AbpMcpServerOptions"/>'s own and every one in
/// <see cref="AbpMcpServerOptions.Endpoints"/> - and checks, once, that every module is served by exactly one of them.
/// <para>
/// <b>An unassigned module is an error, not a module for the default endpoint.</b> Splitting a server exists to keep
/// audiences apart - typically an anonymous public endpoint and one that needs a signed-in user. A module installed
/// later (or a package that starts registering a second namespace) would otherwise land on the default endpoint
/// without anyone deciding it, and the default endpoint is the one most likely to be the public one. So once any
/// endpoint names its modules, every endpoint must, and the application refuses to start until each module is named
/// by one. A server that is never split keeps serving every module at its one endpoint, as before.
/// </para>
/// </summary>
public class AbpMcpEndpointProvider : ISingletonDependency
{
    private readonly Lazy<IReadOnlyList<AbpMcpEndpoint>> _endpoints;

    protected AbpMcpServerOptions Options { get; }

    protected AbpMcpModuleRegistry Registry { get; }

    public AbpMcpEndpointProvider(IOptions<AbpMcpServerOptions> options, AbpMcpModuleRegistry registry)
    {
        Options = options.Value;
        Registry = registry;
        _endpoints = new Lazy<IReadOnlyList<AbpMcpEndpoint>>(CreateEndpoints);
    }

    /// <summary>Every endpoint, the default one first.</summary>
    /// <exception cref="AbpException">Thrown, with every problem found, when the endpoints are inconsistent.</exception>
    public IReadOnlyList<AbpMcpEndpoint> Endpoints => _endpoints.Value;

    /// <summary>The endpoint at <see cref="AbpMcpServerOptions.RoutePattern"/>.</summary>
    public AbpMcpEndpoint DefaultEndpoint => Endpoints[0];

    /// <summary>
    /// Whether the server is split: its modules are assigned to endpoints, and each endpoint shows only its own.
    /// When it is not, nothing about a request depends on the endpoint.
    /// </summary>
    public bool IsSplit => !DefaultEndpoint.ServesEveryModule;

    /// <summary>
    /// The endpoint mapped at <paramref name="path"/> - a literal path such as <c>/admin-mcp</c>, compared as routing
    /// compares it (case-insensitive, ignoring a trailing slash). <c>null</c> when no endpoint has that route.
    /// </summary>
    public virtual AbpMcpEndpoint? FindByPath(string? path)
    {
        if (path == null)
        {
            return null;
        }

        var normalized = NormalizeRoute(path);
        return Endpoints.FirstOrDefault(endpoint =>
            string.Equals(NormalizeRoute(endpoint.RoutePattern), normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks the endpoints (by resolving them) and the transport they share. Called at application initialization,
    /// before any endpoint is mapped.
    /// </summary>
    public virtual void Validate(HttpServerTransportOptions transportOptions)
    {
        // A session is created at one endpoint, with that endpoint's view of the server, but the SDK looks a session
        // up by its id alone - so a client could carry a session created at one endpoint to another, past that
        // endpoint's conventions (its rate limit, its stricter policy). Stateless, there are no sessions to carry.
        if (IsSplit && transportOptions.SessionMode != HttpServerSessionMode.Stateless)
        {
            throw new AbpException(
                $"The MCP server is split across endpoints ({string.Join(", ", Endpoints)}), which needs the stateless " +
                $"transport, but HttpServerTransportOptions.SessionMode is {transportOptions.SessionMode}. A session " +
                "created at one endpoint could be resumed at another, with the first endpoint's tools.");
        }
    }

    protected virtual IReadOnlyList<AbpMcpEndpoint> CreateEndpoints()
    {
        if (Options.Modules.Count == 0 && Options.Endpoints.Count == 0)
        {
            return new[]
            {
                new AbpMcpEndpoint(
                    Options.RoutePattern,
                    isDefault: true,
                    modules: null,
                    AbpMcpInstructions.Compose(Options.Instructions, Registry.Modules),
                    Options.MaxRequestBodySize,
                    Options.EndpointConventions)
            };
        }

        var errors = new List<string>();
        var owners = new Dictionary<AbpMcpModule, string>();
        var routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var endpoints = new List<AbpMcpEndpoint>();

        // The default endpoint may name no module: everything can be served elsewhere, and what is left here is
        // whatever a host registered outside any module (AllowUnownedPrimitives), if anything.
        var defaultModules = ResolveModules(Options.RoutePattern, Options.Modules, owners, errors);
        endpoints.Add(new AbpMcpEndpoint(
            Options.RoutePattern,
            isDefault: true,
            defaultModules,
            AbpMcpInstructions.Compose(Options.Instructions, defaultModules),
            Options.MaxRequestBodySize,
            Options.EndpointConventions));
        routes.Add(NormalizeRoute(Options.RoutePattern));

        foreach (var endpointOptions in Options.Endpoints)
        {
            if (!routes.Add(NormalizeRoute(endpointOptions.RoutePattern)))
            {
                errors.Add($"Two MCP endpoints are mapped at '{endpointOptions.RoutePattern}'.");
            }

            if (endpointOptions.Modules.Count == 0)
            {
                errors.Add(
                    $"The MCP endpoint '{endpointOptions.RoutePattern}' names no module. Add the modules it serves to " +
                    "its Modules, by the name each registered with AddAbpMcpModule.");
            }

            var modules = ResolveModules(endpointOptions.RoutePattern, endpointOptions.Modules, owners, errors);
            endpoints.Add(new AbpMcpEndpoint(
                endpointOptions.RoutePattern,
                isDefault: false,
                modules,
                AbpMcpInstructions.Compose(endpointOptions.Instructions, modules),
                endpointOptions.MaxRequestBodySize ?? Options.MaxRequestBodySize,
                endpointOptions.EndpointConventions,
                endpointOptions.ConfigureResourceMetadata));
        }

        var unassigned = Registry.Modules.Where(module => !owners.ContainsKey(module)).ToList();
        if (unassigned.Count > 0)
        {
            errors.Add(
                $"The MCP module(s) {string.Join(", ", unassigned.Select(module => $"'{module.Name}'"))} are served by " +
                "no endpoint. Once the server is split, every module must be named by exactly one endpoint: add each " +
                $"to AbpMcpServerOptions.Modules (served at '{Options.RoutePattern}') or to the Modules of one of " +
                "AbpMcpServerOptions.Endpoints.");
        }

        if (errors.Count > 0)
        {
            throw new AbpException(
                "The MCP server's endpoints are inconsistent:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(error => " - " + error)));
        }

        return endpoints;
    }

    /// <summary>
    /// The registered modules <paramref name="names"/> refers to, in registration order - the order their
    /// instruction sections are composed in, whatever order the host listed them in.
    /// </summary>
    protected virtual IReadOnlyList<AbpMcpModule> ResolveModules(
        string routePattern,
        IEnumerable<string> names,
        Dictionary<AbpMcpModule, string> owners,
        List<string> errors)
    {
        var modules = new HashSet<AbpMcpModule>();
        foreach (var name in names.Distinct(StringComparer.Ordinal))
        {
            var module = Registry.Find(name);
            if (module == null)
            {
                errors.Add(
                    $"The MCP endpoint '{routePattern}' names the module '{name}', which no module registered. " +
                    "Registered: " + (Registry.Modules.Count == 0
                        ? "none."
                        : string.Join(", ", Registry.Modules.Select(registered => $"'{registered.Name}'")) + "."));
                continue;
            }

            if (owners.TryGetValue(module, out var owner))
            {
                errors.Add(
                    $"The MCP module '{name}' is named by both the '{owner}' and the '{routePattern}' endpoint. A module " +
                    "is served by one endpoint only.");
                continue;
            }

            owners[module] = routePattern;
            modules.Add(module);
        }

        return Registry.Modules.Where(modules.Contains).ToList();
    }

    protected static string NormalizeRoute(string route)
    {
        return "/" + route.Trim().Trim('/');
    }
}
