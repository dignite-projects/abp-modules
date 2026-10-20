# Dignite.Abp.AspNetCore.Mcp

> Part of [**dignite-projects/abp-modules**](https://github.com/dignite-projects/abp-modules) — see
> the [repository README](../README.md) for the other modules, and
> [CONTRIBUTING.md](../CONTRIBUTING.md) for the build, versioning, and release process shared across
> them.

Hosts one **[Model Context Protocol](https://modelcontextprotocol.io) (MCP) server** inside an
**[ABP Framework](https://abp.io)** application, and lets any number of ABP modules contribute tools,
resources and prompts to it. It is infrastructure, not a module with a domain model of its own: it does
not reference, and is not owned by, any module in this repository.

> **.NET 10 · ABP 10.7 · ModelContextProtocol 2.1.0 · LGPL-3.0-only**

The C# MCP SDK keeps exactly **one server per service container**: every module's tools land in the same
server, while the server info, the instructions, the transport, the endpoint and every request filter exist
once — and the SDK drops a second tool with an existing name without an error. This package splits the roles
the way ABP does for SignalR: it owns the server (stateless Streamable HTTP at `/mcp`, ABP-permission
filtering, a unit of work per call, one structured error envelope, startup validation of every module's
namespace), each feature module's `*.Mcp` project only contributes its primitives, and the host only
configures deployment.

## Documentation

**[Read the documentation](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/aspnetcore-mcp/index.md)**
— contributing tools, resources and prompts from a module (namespaces, dynamic resources), hosting the server
(options, bearer authentication with RFC 9728 discovery, third-party tool libraries), and errors and the unit of
work.

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

Every tool and prompt name must start with `my_module_`, and every resource URI must use the `my-module://`
scheme; the server refuses to start on any overlap between modules.

Registered namespaces in this ecosystem: `site` (Dignite.Site), `vault_extract` (Dignite.Vault.Extract,
scheme `vault-extract`).

## Hosting the server (an application)

```csharp
Configure<AbpMcpServerOptions>(options =>
{
    options.ServerName = "My Application";      // default: the ABP application name
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

Authentication is the host's existing bearer scheme, through the endpoint's default policy.

## License

[LGPL-3.0-only](https://github.com/dignite-projects/abp-modules/blob/main/LICENSE).
