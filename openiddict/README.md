# Dignite.Abp.OpenIddict

> Part of [**dignite-projects/abp-modules**](https://github.com/dignite-projects/abp-modules) — see
> the [repository README](../README.md) for the other modules, and
> [CONTRIBUTING.md](../CONTRIBUTING.md) for the build, versioning, and release process shared across
> them.

Additions to the **[ABP Framework](https://abp.io) OpenIddict module** for an ABP authorization server. It is
infrastructure, not a module with a domain model of its own: it does not reference, and is not referenced by, any
other tree in this repository.

> **.NET 10 · ABP 10.7 · OpenIddict 7.7 · LGPL-3.0-only**

## Dignite.Abp.OpenIddict.ClientIdMetadataDocuments

Lets the authorization server accept OAuth **Client ID Metadata Documents** (CIMD), the client registration the
[MCP authorization specification](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/client-registration)
recommends and ChatGPT's and Claude's connectors use. A client's `client_id` is an HTTPS URL; the server fetches the
JSON document there — https only, no redirects, no proxy, public addresses only, 5 seconds, 5 KB — and registers the
client in OpenIddict's own store as a public PKCE client for as long as the document's `Cache-Control` says. No
pre-registration, no dynamic client registration endpoint. The discovery document advertises the support, and every
resource the server registered with `RegisterResources` is granted to the client as an `rsrc:` permission.

It is built from two OpenIddict event handlers only, so it can be retired once OpenIddict supports CIMD itself. It
belongs on the authorization server, which need not host an MCP server: it does not depend on
`Dignite.Abp.AspNetCore.Mcp`.

```csharp
[DependsOn(typeof(AbpOpenIddictClientIdMetadataDocumentsModule))]
public class MyAuthServerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpClientIdMetadataDocumentOptions>(options =>
        {
            options.Scopes.Add("MyApp");                // required: the scopes a CIMD client may request
            options.AllowedHosts.Add("claude.ai");      // recommended in production
            options.AllowedHosts.Add("chatgpt.com");
        });
    }
}
```

## Documentation

**[Read the documentation](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/openiddict/client-id-metadata-documents.md)**
— how it works, the options, resource indicators, production settings, pre-registered clients, the consent page
name, multi-tenancy and the cleanup of expired registrations.

## License

[LGPL-3.0-only](https://github.com/dignite-projects/abp-modules/blob/main/LICENSE).
