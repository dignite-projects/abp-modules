```json
//[doc-seo]
{
    "Description": "Host one Model Context Protocol (MCP) server in an ABP application and let each module contribute AI-callable tools, resources and prompts to it in its own namespace."
}
```

# MCP Server

`Dignite.Abp.AspNetCore.Mcp` hosts **one [Model Context Protocol](https://modelcontextprotocol.io) (MCP) server** in an ABP application, and lets any number of modules contribute **tools**, **resources** and **prompts** to it. An AI assistant such as Claude or ChatGPT connects to the server and calls the tools as the signed-in user, with that user's permissions.

It is infrastructure, not a module with a domain model of its own: it knows no business concept, and it is not owned by any module. It is built on the [official C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk).

## Why It Exists

The C# MCP SDK keeps exactly **one server per service container**. Every module's tools land in the same server, while the server's name, its instructions, its transport, its endpoint and its request filters exist only once. A module that configures any of those configures the whole server, and the next module that does collides with it. On top of that, the SDK resolves name collisions silently: a second tool with an existing name is dropped without an error, and which one survives depends on the module order.

This package splits the roles the way ABP does for [SignalR](https://abp.io/docs/latest/framework/real-time/signalr), where ABP maps the hubs and modules only supply them:

| | Owns | Never touches |
|---|---|---|
| **This package** | The server, the stateless Streamable HTTP transport, the `/mcp` endpoint, permission filtering, a unit of work per call, one structured error format, the server's name and composed instructions, startup validation, optional authentication discovery | Any business concept |
| **A module's `*.Mcp` project** | Its tool, resource and prompt classes, its namespace, its section of the instructions | The server's name, transport, endpoint, global filters |
| **The application** | Deployment: the route, the server name, authentication, rate limiting | MCP server code |

## How It Works

* **Transport**: Stateless Streamable HTTP, at `/mcp`. Every MCP request is an ordinary HTTP request through the application's pipeline, so authentication, tenant resolution and the unit of work are in place before a tool runs.
* **Authorization**: The endpoint requires an authenticated user. `[Authorize]` on a tool, a resource or a prompt is honored twice: what the user may not call does not appear in the list results, and a call that slips through is refused. ABP resolves a permission name as a policy, so `[Authorize(MyPermissions.Products.Default)]` works as it does on an application service.
* **Namespaces**: Each module claims a namespace, a tool name prefix and a resource URI scheme, and the server **refuses to start** on any overlap. See [Contributing Tools](contributing-tools.md#namespaces).
* **Errors and transactions**: An exception becomes a structured error result the model can act on, and each call runs in its own unit of work. See [Errors and Unit of Work](errors-and-unit-of-work.md).

## Packages

| Package | Description |
|---|---|
| `Dignite.Abp.AspNetCore.Mcp` | The server, `AddAbpMcpModule` for modules, and the options and authentication discovery for applications. Built on `ModelContextProtocol.AspNetCore` 2.1. |

A module's `*.Mcp` project depends on this package and on the module's application contracts; an application gets it with the modules it installs.

## See Also

* [Contributing Tools](contributing-tools.md)
* [Hosting the Server](hosting.md)
* [Errors and Unit of Work](errors-and-unit-of-work.md)
