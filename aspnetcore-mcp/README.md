# Dignite.Abp.AspNetCore.Mcp

> Part of [**dignite-projects/abp-modules**](https://github.com/dignite-projects/abp-modules) — see
> the [repository README](../README.md) for the other modules, and
> [CONTRIBUTING.md](../CONTRIBUTING.md) for the build, versioning, and release process shared across
> them.

Hosts one **[Model Context Protocol](https://modelcontextprotocol.io) (MCP) server** inside an
**[ABP Framework](https://abp.io)** application, and lets any number of ABP modules contribute tools,
resources and prompts to it. It is infrastructure, not a module with a domain model of its own: it does
not reference, and is not owned by, any module in this repository.

> **.NET 10 · ABP 10.5.0 · ModelContextProtocol 2.1.0 · LGPL-3.0-only**

## Why it exists

The C# MCP SDK keeps exactly **one server per service container**. Every module's `WithTools<T>()`
lands in the same server, while the server info, the instructions, the transport, the endpoint and every
request filter exist once. A feature module that configures any of those is configuring the whole
server, and the second such module collides with it. On top of that, the SDK resolves name collisions
silently: a second tool with an existing name, or a second resource with an existing URI template, is
dropped without an error, and which one survives depends on module load order.

This package splits the two roles the way ABP does for SignalR (`AbpAspNetCoreSignalRModule` maps the
hubs; feature modules only supply them):

| | Owns | Never touches |
|---|---|---|
| **`AbpAspNetCoreMcpModule`** (this package) | The server, stateless Streamable HTTP transport, the `/mcp` endpoint, ABP-permission filtering of list results, one structured error envelope, server info, composed instructions, startup validation, optional RFC 9728 discovery | Any business concept |
| **A feature module's `*.Mcp` project** | Its tool/resource/prompt classes, its namespace, its section of the instructions | Server info, transport, endpoint, global filters, server-wide `With*Handler` slots |
| **The host** | Deployment: route, server name, the bearer scheme, discovery metadata, rate limiting | MCP server code |

## Contributing to the server (a feature module)

```csharp
[DependsOn(typeof(MyApplicationContractsModule), typeof(AbpAspNetCoreMcpModule))]
public class MyMcpModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpMcpModule("my_module", mcp => mcp
            .AddTools<MyTools>()
            .AddResources<MyResources>()
            .AddInstructions("What my_module_* tools are for, and how to start."));
    }
}
```

Tool classes use the SDK's own attributes (`[McpServerToolType]`, `[McpServerTool]`, `[Description]`)
and ASP.NET Core's `[Authorize(MyPermissions.X)]`, which ABP resolves as a permission. A tool should call
the module's application services — it is another caller of them, not a second implementation.

### Namespaces — how names stay unique

`AddAbpMcpModule(name, …)` claims a namespace, and the server **refuses to start** if anything breaks
it:

- every **tool** and **prompt** name must start with `{name}_` (`my_module_list_items`);
- every **resource** URI must use the module's scheme — by default the name with `_` turned into `-`
  (`my-module://…`), or set one with `UseUriScheme(...)`;
- module names are lower-case snake_case and must not nest (`file` and `file_explorer` cannot coexist);
- two modules may not share a URI scheme;
- a tool, resource or prompt registered outside `AddAbpMcpModule` (the SDK's raw `WithTools<T>()`) is
  rejected, since no namespace owns it - unless the host sets `AbpMcpServerOptions.AllowUnownedPrimitives`,
  the way to host a third-party MCP tool library that cannot be changed (duplicates are refused either way);
- any duplicate name or URI template fails, naming both registrations.

Because the rules are checked against a module's own registrations, a module's tests catch a violation
before any host combines it with another module.

Registered namespaces in this ecosystem: `site` (Dignite.Site), `file_explorer`
(Dignite.FileExplorer.Mcp, scheme `file-explorer`), `vault_extract` (Dignite.Vault.Extract, scheme
`vault-extract`).

### Dynamic resources in `resources/list`

Static `[McpServerResource]` registrations are listed by the SDK itself. For resources that are the
caller's data (one entry per document type, per cabinet…), implement `IAbpMcpResourceListContributor` and
register it with `AddResourceListContributor<T>()`. Do not use the SDK's `WithListResourcesHandler`: it
is one slot on the shared server, and the next module to set it replaces yours. Contributors are resolved
from the request scope, must authorize what they list, return `null`/empty rather than throw when the
caller may see nothing (an exception fails the listing for every module), and may only list URIs in their
module's scheme.

### Why `AddTools<T>()` rather than the SDK's `WithTools<T>()`

Besides recording ownership, it resolves the tool class from ABP's container on every call. The SDK
constructs it with `ActivatorUtilities` instead, so a replacement registered with
`[Dependency(ReplaceServices = true)]` is never used and overriding a virtual tool method silently does
nothing.

## Hosting the server (an application)

Depend on the feature modules you want; this module comes with them. Then configure deployment:

```csharp
Configure<AbpMcpServerOptions>(options =>
{
    options.ServerName = "My Application";      // default: the ABP application name
    options.Instructions = "This server manages ...";  // optional opening section
    options.MaxRequestBodySize = 4 * 1024 * 1024;     // the default; modules may raise it for themselves
    options.EndpointConventions.Add(endpoint => endpoint.RequireRateLimiting("mcp"));
});

// Lets an MCP client without a token discover where to authenticate (RFC 9728):
context.Services.AddAbpMcpAuthenticationDiscovery(metadata =>
{
    metadata.Resource = configuration["App:SelfUrl"];
    metadata.AuthorizationServers.Add(configuration["AuthServer:Authority"]);
    metadata.ScopesSupported.Add("MyApp");
});
```

The pipeline needs `UseRouting()`, `UseAuthentication()`, `UseAuthorization()` and
`UseConfiguredEndpoints()`, as any ABP host has.

**Authentication is the host's existing bearer scheme**, through the endpoint's default policy. The
endpoint deliberately does not name an authentication scheme: naming one makes ASP.NET Core
re-authenticate through it and replace the principal that ABP's dynamic claims middleware has already
refreshed, so role changes and revocations made after a token was issued would not reach MCP requests.
`AddAbpMcpAuthenticationDiscovery` therefore only takes over the **challenge** — an unauthenticated MCP
request gets `401` with `WWW-Authenticate: Bearer resource_metadata="…"` instead of the host's default
(in an ABP MVC host, a `302` to the login page that no MCP client can follow).

It does that by wrapping the host's `IAuthorizationMiddlewareResultHandler` (keeping its lifetime), so a
host with a handler of its own registers it *before* calling `AddAbpMcpAuthenticationDiscovery`; one
registered afterwards would replace the wrapper, and the application refuses to start rather than let the
MCP 401 silently turn back into a login redirect. Calling it again only replaces the metadata.

## Errors

Any exception a tool throws becomes a `CallToolResult` with `isError: true` and a structured payload,
built with ABP's own `IExceptionToErrorInfoConverter`:

```json
{ "error": { "kind": "validation", "code": null, "message": "…",
             "validationErrors": [ { "message": "…", "fields": [ "slug" ] } ] } }
```

`kind` is one of `validation`, `notFound`, `conflict`, `forbidden`, `unknown`. An exception whose type
would be classified wrongly — typically a `UserFriendlyException` that means "not found" — implements
`IHasMcpToolErrorKind` to say so.

The SDK's own exceptions keep the SDK's semantics: an `McpException`'s message is passed through as
written (ABP's converter would replace it with a generic one), while `McpProtocolException` (a
protocol-level failure, such as an unknown tool) and `InputRequiredException` (a tool asking the client for
more input mid-call) are not converted at all.
