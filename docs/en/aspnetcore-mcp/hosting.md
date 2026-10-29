```json
//[doc-seo]
{
    "Description": "Host the MCP server in an ABP application: the server options, bearer authentication with RFC 9728 discovery, rate limiting, request size and third-party tool libraries."
}
```

# Hosting the Server

An application gets the MCP server with the modules it installs: depend on their `*.Mcp` modules, and `AbpAspNetCoreMcpModule` comes with them. The server is mapped at `/mcp` by ABP's endpoint configuration, so the pipeline only needs what every ABP application has: `UseRouting()`, `UseAuthentication()`, `UseAuthorization()` and `UseConfiguredEndpoints()`.

## Options

Configure the server with `AbpMcpServerOptions`, in the `ConfigureServices` method of your module:

````csharp
Configure<AbpMcpServerOptions>(options =>
{
    options.ServerName = "My Application";
    options.Instructions = "This server manages the product catalog of My Application.";
    options.EndpointConventions.Add(endpoint => endpoint.RequireRateLimiting("mcp"));
});
````

| Option | Default | Description |
|---|---|---|
| `RoutePattern` | `/mcp` | The route of the endpoint. |
| `ServerName` | The ABP application name | The name the server reports to clients. |
| `ServerVersion` | `1.0.0` | The version the server reports to clients. |
| `Instructions` | `null` | An opening section of the instructions, before the sections of the modules. |
| `MaxRequestBodySize` | 4 MB | The largest request body accepted, enforced before the body is read, so an oversized request gets `413` instead of being buffered first. `null` leaves the server's own limit (30 MB on Kestrel). A module whose tools take large arguments, such as base64 uploads, raises it. |
| `EndpointConventions` | Empty | Conventions applied to the endpoint after its authorization: rate limiting, CORS, a stricter policy. |
| `AllowUnownedPrimitives` | `false` | See [Third-Party Tool Libraries](#third-party-tool-libraries). |

## Authentication

The endpoint uses **the application's existing bearer authentication**, through its default authorization policy: an MCP client sends the same access token an API client would, and the tools run as that user.

The endpoint deliberately names no authentication scheme. Naming one would make ASP.NET Core authenticate the request again and replace the principal that ABP's [dynamic claims](https://abp.io/docs/latest/framework/fundamentals/dynamic-claims) middleware has already refreshed, so role changes and revocations made after a token was issued would not reach MCP requests.

### Authentication Discovery

An MCP client without a token needs to find out where to sign in. `AddAbpMcpAuthenticationDiscovery` serves the [RFC 9728](https://datatracker.ietf.org/doc/html/rfc9728) protected resource metadata for that:

````csharp
context.Services.AddAbpMcpAuthenticationDiscovery(metadata =>
{
    metadata.Resource = configuration["App:SelfUrl"];
    metadata.AuthorizationServers.Add(configuration["AuthServer:Authority"]);
    metadata.ScopesSupported.Add("MyApplication");
});
````

It only takes over the **challenge**: an unauthenticated MCP request gets `401` with `WWW-Authenticate: Bearer resource_metadata="...", scope="MyApplication"`, instead of the application's default, which in an MVC application is a `302` to the login page that no MCP client can follow.

It does that by wrapping the application's `IAuthorizationMiddlewareResultHandler`. An application with a handler of its own registers it **before** calling `AddAbpMcpAuthenticationDiscovery`; a handler registered afterwards would replace the wrapper, and the application refuses to start rather than let the MCP `401` silently turn back into a login redirect. Calling it again only replaces the metadata.

The `scope` part is the metadata's `ScopesSupported`, joined with spaces. A client takes the scope to ask the authorization server for from the challenge first, then from the metadata ([MCP authorization, scope selection](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization)); some clients that find neither ask for every scope the authorization server lists, and an authorization server that only allows the MCP client a few of them refuses the request. Saying it in the challenge as well as in the metadata leaves no such client to guess. Nothing is added when `ScopesSupported` is empty.

## Multi-Tenancy

Every MCP request goes through the normal pipeline, so the tenant is resolved as for any other request, by the token's tenant claim or the application's other [tenant resolvers](https://abp.io/docs/latest/framework/architecture/multi-tenancy#tenant-resolvers), before a tool runs.

## Third-Party Tool Libraries

By default, a tool, resource or prompt registered outside any module's namespace, with the SDK's own `WithTools<T>()` and so on, stops the application from starting, because nothing keeps the next module from colliding with it. To host a third-party MCP tool library that you cannot change, set `AllowUnownedPrimitives`:

````csharp
Configure<AbpMcpServerOptions>(options =>
{
    options.AllowUnownedPrimitives = true;
});
````

Duplicate names are still refused.

## See Also

* [Contributing Tools](contributing-tools.md)
* [Errors and Unit of Work](errors-and-unit-of-work.md)
