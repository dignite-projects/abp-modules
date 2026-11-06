```json
//[doc-seo]
{
    "Description": "Host the MCP server in an ABP application: the server options, several endpoints for different audiences, bearer authentication with RFC 9728 discovery, rate limiting, request size and third-party tool libraries."
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
| `RoutePattern` | `/mcp` | The route of the endpoint (the default endpoint, when there are [several](#several-endpoints)). |
| `ServerName` | The ABP application name | The name the server reports to clients. |
| `ServerVersion` | `1.0.0` | The version the server reports to clients. |
| `Instructions` | `null` | An opening section of the instructions, before the sections of the modules. |
| `MaxRequestBodySize` | 4 MB | The largest request body accepted, enforced before the body is read, so an oversized request gets `413` instead of being buffered first. `null` leaves the server's own limit (30 MB on Kestrel). A module whose tools take large arguments, such as base64 uploads, raises it. |
| `EndpointConventions` | Empty | Conventions applied to the endpoint after its authorization: rate limiting, CORS, a stricter policy. |
| `Modules` | Empty | The modules served at `RoutePattern`, when the server has [several endpoints](#several-endpoints). Empty, with no `Endpoints`, means every module. |
| `Endpoints` | Empty | Further endpoints of the same server. See [Several Endpoints](#several-endpoints). |
| `AllowUnownedPrimitives` | `false` | See [Third-Party Tool Libraries](#third-party-tool-libraries). |
| `SerializerOptions` | A copy of the SDK's JSON options that writes most characters as they are | The JSON options of every tool, resource and prompt registered with `AddAbpMcpModule`: tools write their results and tools and prompts read their arguments with them. See [Tool Results as JSON](#tool-results-as-json). |

## Several Endpoints

The server can be mapped at several routes, each showing only the modules it names. Do this when the modules have different audiences: a public calculation module that anyone may call next to an administration module for signed-in users is the usual pair. On one endpoint, an anonymous client would list the administration tools too (each still checks its permission, some only when called) and read their instructions on connect.

````csharp
Configure<AbpMcpServerOptions>(options =>
{
    // The default endpoint, at RoutePattern (/mcp): public and rate limited.
    options.Modules.Add("calc");
    options.Instructions = "Calculations of My Application.";
    options.EndpointConventions.Add(endpoint => endpoint.AllowAnonymous());
    options.EndpointConventions.Add(endpoint => endpoint.RequireRateLimiting("mcp"));

    // A second endpoint for the website's administration: signed-in users only.
    options.Endpoints.Add(new AbpMcpEndpointOptions("/site-mcp")
    {
        Modules = { "site" },
        Instructions = "Administration of the My Application website.",
        ConfigureResourceMetadata = metadata => metadata.ResourceName = "My Application website"
    });
});
````

A module is named by the name it registered with `AddAbpMcpModule`, the prefix of its tool names. Each endpoint is the same server, so some things are its own and some are shared:

| Each endpoint has its own | Every endpoint shares |
|---|---|
| Route | Server name and version |
| Modules: their tools, resources, prompts and `resources/list` contributors | Startup validation of names and namespaces |
| Instructions: its opening section, then the sections of its modules | `SerializerOptions` |
| Endpoint conventions | Permission filtering, error format, unit of work |
| Request body limit | The authentication scheme and authorization server |
| [Resource metadata](#authentication-discovery) document and `401` challenge | |

`AbpMcpServerOptions`' own `RoutePattern`, `Instructions`, `EndpointConventions`, `MaxRequestBodySize` and `Modules` configure the **default endpoint**. Each `AbpMcpEndpointOptions` has the same settings for its endpoint:

| Option | Default | Description |
|---|---|---|
| `RoutePattern` | Required | The route of the endpoint. |
| `Modules` | Empty | The modules it serves. At least one. |
| `Instructions` | `null` | The opening section of its instructions. The server's `Instructions` open the default endpoint's only. |
| `MaxRequestBodySize` | `null` | `null` takes the server's `MaxRequestBodySize`, so a module that raised that limit for its arguments keeps the room on whichever endpoint serves it. |
| `EndpointConventions` | Empty | Its conventions. The server's `EndpointConventions` are **not** applied here: an anonymous public endpoint must not make the next one anonymous too. |
| `ConfigureResourceMetadata` | `null` | Changes its copy of the resource metadata. See [Discovery per Endpoint](#discovery-per-endpoint). |

### Assigning Modules

The server is split as soon as `Modules` or `Endpoints` names anything. From then on **every module must be named by exactly one endpoint**, and the application refuses to start when one is not. It also refuses a module named by two endpoints, a name no module registered, an endpoint in `Endpoints` that names no module, and two endpoints at one route. Every problem is reported at once.

A module that no endpoint names is an error, not a module for the default endpoint. The split exists to keep audiences apart, and the default endpoint is often the public one: a module installed later, or a package that starts registering a second namespace, would otherwise appear there without anyone deciding it. Name it where it belongs.

The default endpoint may name no module, when everything is served elsewhere. Tools, resources and prompts registered outside any module, allowed by [`AllowUnownedPrimitives`](#third-party-tool-libraries), belong to no namespace, and only the default endpoint serves them.

A server whose `Modules` and `Endpoints` are both empty has one endpoint that serves every module, exactly as before endpoints could be split.

### What a Client of One Endpoint Sees

Every request is served by the part of the server its endpoint shows. `tools/list`, `resources/list`, resource templates and `prompts/list` list only the endpoint's own modules, and calling a tool, reading a resource or getting a prompt of another endpoint's module fails as it would for a name that does not exist. The instructions are composed from the endpoint's opening section and its modules' sections only.

This is not a replacement for permissions. An endpoint decides what a client sees and which conventions, such as authentication, apply; each tool still checks its own permissions, at every endpoint.

### Discovery per Endpoint

With [authentication discovery](#authentication-discovery), every endpoint's `401` points at its own document, as [RFC 9728](https://datatracker.ietf.org/doc/html/rfc9728#section-3.1) places it: `/.well-known/oauth-protected-resource/site-mcp` for `/site-mcp`. The default endpoint's document is the one `AddAbpMcpAuthenticationDiscovery` configured. Each other endpoint serves a copy of it, in which:

* `resource` names the endpoint, because a client rejects a document about another resource. A configured resource that ends with the default endpoint's route has it replaced (`https://api.example.com/mcp` becomes `https://api.example.com/site-mcp`); any other, such as an origin, has the endpoint's route appended.
* `ConfigureResourceMetadata` then changes what it needs to, for example `ScopesSupported` or `ResourceName`, or `Resource` itself.

The `scope` of the endpoint's challenge is that copy's `ScopesSupported`.

Setting `McpAuthenticationOptions.ResourceMetadataUri` gives every endpoint one document at one address: the endpoints' challenges all point there, and it is the configured document.

### Limitations

* **The transport must be stateless**, the SDK's default. A session is created at one endpoint, with its view of the server, but looked up by its id alone, so it could be resumed at another endpoint, past that endpoint's conventions. A split server with `HttpServerTransportOptions.SessionMode` set to anything else refuses to start.
* The server's name and version are the same at every endpoint.
* The narrowing runs in the SDK's `HttpServerTransportOptions.ConfigureSessionOptions`. A callback the application sets there itself still runs, afterwards, and sees the endpoint's part of the server. A route mapped with the SDK's own `MapMcp` is not one of the endpoints, and a split server refuses its requests rather than serving every module there.
* A module that raises the server's `MaxRequestBodySize` for its own arguments raises the default endpoint's limit too, whichever endpoint serves the module, as it did when there was one endpoint. To keep the default endpoint's limit low, give the module's endpoint a `MaxRequestBodySize` of its own that is large enough, and set the server's back in an `IPostConfigureOptions<AbpMcpServerOptions>` of your own, registered after the module's.

A process per audience, each with its own server at its own route, is the alternative when the modules are deployed apart anyway.

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

A server with [several endpoints](#several-endpoints) serves a document per endpoint, see [Discovery per Endpoint](#discovery-per-endpoint).

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
