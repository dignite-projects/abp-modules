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
| `SerializerOptions` | A copy of the SDK's JSON options that writes most characters as they are | The JSON options of every tool, resource and prompt registered with `AddAbpMcpModule`: tools write their results and tools and prompts read their arguments with them. See [Tool Results as JSON](#tool-results-as-json). |

## Tool Results as JSON

A tool that returns a DTO, a list or any other object has it written as JSON text in its result, and that text is what the model reads. The server writes it with `AbpMcpServerOptions.SerializerOptions`. By default these are the MCP SDK's own JSON options, copied: web naming, enums as strings and `null` properties left out, exactly as the SDK writes them, with one difference: most characters are written as they are, not as `\uXXXX` escapes.

The SDK's own encoder writes every non-ASCII character, and `<`, `>`, `&`, `'`, `"`, `+` and the backtick, as `\uXXXX`. Chinese text then reaches the model as `\u5F00\u59CB`, HTML as `\u003Cdiv class=\u0022x\u0022\u003E` and the `+` of a time zone offset as `\u002B`, several times longer than the text itself, which costs tokens and makes it hard to read:

````text
SDK default:  {"title":"\u5F00\u59CB","html":"\u003Cdiv class=\u0022x\u0022\u003E"}
This server:  {"title":"开始","html":"<div class=\"x\">"}
````

Not escaping them is safe here, because the text is JSON in a text block of the result, not markup. The JSON-RPC message that carries the result is still written by the SDK's own options, so the HTTP response is escaped as before. A client that put a tool result into a web page without encoding it would already be exposed to a tool that returns a string, which is passed on as it is.

Some characters are still escaped, because JSON requires it or the encoder does not write them as they are:

* `"` and `\` inside a string, written `\"` and `\\`.
* Control characters.
* Whitespace other than the ASCII space, among them the no-break space U+00A0 and the ideographic space U+3000, which Chinese text often starts a paragraph with.
* The line and paragraph separators U+2028 and U+2029, the byte order mark U+FEFF, and private-use and unassigned code points.
* Every character outside the Basic Multilingual Plane, such as emoji and rare Chinese characters, written as a pair of escapes (`\uD83D\uDE00` for U+1F600).

The parsed value is always the same.

### Changing the Options

Add to the default rather than replacing it, for example a converter for a type of your own:

````csharp
Configure<AbpMcpServerOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new MoneyJsonConverter());
});
````

An instance made with `new JsonSerializerOptions()` has none of the SDK's contracts, so DTOs would be written differently. To get the SDK's escaping back, start from its options instead:

````csharp
Configure<AbpMcpServerOptions>(options =>
{
    options.SerializerOptions = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
});
````

To change what is escaped, set `Encoder` on the options. An encoder made with `JavaScriptEncoder.Create(UnicodeRanges.All)` still escapes the ideographic space and every character outside the Basic Multilingual Plane, so writing those as they are takes an encoder of your own, derived from `JavaScriptEncoder`.

The options are shared by all primitives and become read-only when the first one is built, so set them in `Configure<AbpMcpServerOptions>` and not while the application runs. A tool, resource or prompt registered with the SDK's own `WithTools<T>()` and so on, see [Third-Party Tool Libraries](#third-party-tool-libraries), is written with the options that registration was given.

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
