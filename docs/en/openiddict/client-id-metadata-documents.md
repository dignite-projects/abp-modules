```json
//[doc-seo]
{
    "Description": "Let an ABP authorization server built on OpenIddict accept OAuth Client ID Metadata Documents, the client registration ChatGPT and Claude use for MCP connectors: no pre-registration and no dynamic client registration."
}
```

# Client ID Metadata Documents

`Dignite.Abp.OpenIddict.ClientIdMetadataDocuments` lets an ABP authorization server built on OpenIddict accept **OAuth Client ID Metadata Documents** (CIMD). A client uses an HTTPS URL as its `client_id`; the server fetches the JSON document at that URL to learn the client's name and redirect URIs. Nothing has to be registered in advance, and no dynamic client registration (RFC 7591) endpoint is opened.

This is the registration the [MCP authorization specification](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/client-registration) recommends, and the one ChatGPT's and Claude's web connectors use. It is defined by [draft-ietf-oauth-client-id-metadata-document](https://datatracker.ietf.org/doc/draft-ietf-oauth-client-id-metadata-document/), still a draft; neither OpenIddict nor ABP supports it yet. The package is built from two OpenIddict event handlers only and replaces none of OpenIddict's services, so it can be removed once OpenIddict supports CIMD itself.

The package belongs on the **authorization server**. It does not depend on `Dignite.Abp.AspNetCore.Mcp`, and the MCP server package does not depend on it: the authorization server and the MCP server are often different applications.

## How It Works

1. The discovery document (`/.well-known/openid-configuration`) says `"client_id_metadata_document_supported": true` and lists `none` among the token endpoint's authentication methods. A client that sees both uses CIMD instead of dynamic registration.
2. An authorization request arrives with a `client_id` such as `https://claude.ai/oauth/mcp-oauth-client-metadata`. Before OpenIddict looks the client up, the package fetches that URL and checks the document: its `client_id` must be the URL itself, it must have a `client_name`, its `redirect_uris` must be `https` or loopback `http`, and `none` must be among the authentication methods it declares. Of the grant types it lists, `authorization_code` is required, and only `authorization_code` and `refresh_token` are granted.
3. The client is registered in OpenIddict's own application store: a public client that must use PKCE, with explicit consent. From there, redirect URI validation, the consent page, the code exchange and refresh are OpenIddict's and ABP's usual flow.
4. The registration lives as long as the document's `Cache-Control` says, within the configured bounds. On the client's next authorization request after that, the document is fetched again and the registration follows it. If the document cannot be fetched for now (a timeout, a `5xx`), the existing registration is kept; if it is fetched and no longer valid, the client is refused.

A `client_id` that is not an `https` URL is looked up as usual, so pre-registered clients are unaffected.

## Installation

Add the `Dignite.Abp.OpenIddict.ClientIdMetadataDocuments` NuGet package to the authorization server project, depend on the module, and say which scopes a CIMD client may request:

````csharp
[DependsOn(typeof(AbpOpenIddictClientIdMetadataDocumentsModule))]
public class MyAuthServerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpClientIdMetadataDocumentOptions>(options =>
        {
            options.Scopes.Add("MyApp");
            options.Scopes.Add("profile");
            options.Scopes.Add("email");
        });
    }
}
````

`Scopes` has no default, because a library cannot know your API's scope: the application refuses to start while the feature is enabled and `Scopes` is empty. `openid` and `offline_access` need no entry; OpenIddict never checks permissions for them.

The options can also be bound from configuration:

````csharp
Configure<AbpClientIdMetadataDocumentOptions>(configuration.GetSection("AuthServer:ClientIdMetadataDocuments"));
````

## Options

| Option | Default | Description |
|---|---|---|
| `IsEnabled` | `true` | Whether an `https` `client_id` is resolved as a metadata document, and the discovery document says so. |
| `Scopes` | Empty (required) | The scopes a CIMD client may request, granted as `scp:` permissions. |
| `AllowedHosts` | Empty | The hosts a `client_id` may be on, compared exactly (no subdomains). Empty accepts any public host. See [Production](#production). |
| `FetchTimeout` | 5 seconds | How long fetching a document may take, connecting included. |
| `MaxDocumentBytes` | 5 KB | The largest document accepted. |
| `MinCacheLifetime` | 5 minutes | The shortest time a registration is trusted, whatever `Cache-Control` says, `no-store` included. |
| `MaxCacheLifetime` | 24 hours | The longest time a registration is trusted. |
| `DefaultCacheLifetime` | 1 hour | The time a registration is trusted when the response has no `max-age`. |
| `RemovePrivateKeyJwtFromDiscovery` | `true` | Whether `private_key_jwt` is taken out of the discovery document's `token_endpoint_auth_methods_supported`. See below. |
| `IsCleanupEnabled` | `true` | Whether a background worker deletes expired registrations. See [Cleanup](#cleanup). |
| `CleanupPeriod` | 1 hour | How often the cleanup worker runs. |

`RemovePrivateKeyJwtFromDiscovery` exists because a client that sees `private_key_jwt` may switch to a confidential-client metadata document and authenticate with a signed assertion (Claude does), while a CIMD registration here is always a public client, so the sign-in fails. The setting changes the **whole** discovery document: if the server has pre-registered confidential clients that authenticate with `private_key_jwt`, turn it off.

## Resource Indicators

MCP clients send an [RFC 8707](https://datatracker.ietf.org/doc/html/rfc8707) `resource` parameter naming the MCP endpoint. A server that checks it registers its resources with `RegisterResources`, and OpenIddict then also requires the client to hold an `rsrc:` permission for each resource it names, failing the request with `ID2192` otherwise.

The package grants those permissions itself: every CIMD registration gets an `rsrc:` permission for each resource in `OpenIddictServerOptions.Resources`, in both forms, with and without the trailing slash, since OpenIddict compares the parameter as a raw string and clients differ in which form they send. Register both forms of each resource on the server as well:

````csharp
PreConfigure<OpenIddictServerBuilder>(builder =>
{
    builder.RegisterResources(
        new Uri("https://api.example.com/mcp"),
        new Uri("https://api.example.com/mcp/"));
});
````

A server that turns resource validation off (`DisableResourceValidation()` and `IgnoreResourcePermissions()`) needs nothing; the permissions are then unused.

The scope and resource permissions follow the server's configuration: a registration takes the current `Scopes` and resources on the client's next authorization request, without waiting for its document to expire.

## Production

**Set `AllowedHosts`** to the clients you expect:

````csharp
Configure<AbpClientIdMetadataDocumentOptions>(options =>
{
    options.AllowedHosts.Add("claude.ai");
    options.AllowedHosts.Add("chatgpt.com");
});
````

With `AllowedHosts` empty, the server follows the specification's "open server" policy: any client on a public host can register, which is what lets a client nobody listed connect, but it also lets anyone who publishes a valid document add a row to the application table (the [cleanup](#cleanup) removes the unused ones) and show a consent page under your domain. The consent page always shows the redirect hosts next to the client name, as the specification requires, so the user sees where the authorization code goes.

Whatever `AllowedHosts` says, fetching a document is guarded against server-side request forgery: `https` only, redirects not followed, no proxy, the host resolved before connecting and refused when any of its addresses is loopback, private, link-local (cloud metadata endpoints included), carrier-grade NAT, documentation, multicast or reserved, plus the time and size limits above. An authorization server that must reach the internet through a proxy cannot fetch documents; give it direct egress for these requests.

## Pre-Registered Clients

CIMD clients and pre-registered clients live side by side in the same application store:

* A `client_id` that is not an `https` URL is never fetched. A pre-registered client for users who add the connector by hand, with its own client ID, keeps working.
* A pre-registered client whose `client_id` happens to be an `https` URL is left alone too: the package only updates registrations it made itself, which it marks with a `cimd_expires_at` property.
* A CIMD registration shows up in ABP's application management UI like any other application. Editing it is pointless: the next fetch overwrites it.

## Consent Page Display Name

The registration's display name, shown on the consent page, is the client name followed by the redirect hosts: `Claude (claude.ai)`. To change the wording or the language, replace the provisioner and override `GetDisplayName`:

````csharp
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(ClientIdMetadataDocumentClientProvisioner))]
public class MyClientIdMetadataDocumentClientProvisioner : ClientIdMetadataDocumentClientProvisioner
{
    public MyClientIdMetadataDocumentClientProvisioner(IAbpLazyServiceProvider lazyServiceProvider)
        : base(lazyServiceProvider)
    {
    }

    protected override string GetDisplayName(string clientName, IReadOnlyList<string> redirectHosts)
    {
        return $"{clientName}（{string.Join("、", redirectHosts)}）";
    }
}
````

The name must still contain every redirect host: a name without one of them is refused, and the client cannot sign in.

## Multi-Tenancy

OpenIddict's applications belong to the host: ABP's `OpenIddictApplication` is not multi-tenant, and its database context ignores the current tenant. A CIMD client is registered in the host even when the authorization request has already resolved a tenant, and the user's tenant is decided at sign-in as usual.

## Cleanup

Every registration that has expired and that no authorization or token refers to is deleted by a background worker, every `CleanupPeriod`, on one instance at a time (through ABP's distributed lock). A client in use keeps its registration; a client that comes back after its registration was deleted is registered again on its next authorization request. The rows are deleted for good, not soft-deleted.

The worker does not run when `IsCleanupEnabled` is `false`, or when ABP's background workers are disabled (`AbpBackgroundWorkerOptions.IsEnabled`); in a deployment with several authorization server instances, keep it on in at least one of them.

## Limitations

* Only the authorization endpoint registers clients. A client that starts with a pushed authorization request (PAR) is not registered; the MCP clients in use today do not.
* Only public clients are registered. A client that can only authenticate with `private_key_jwt` is refused.
* The draft may still change. The package follows the clients' behaviour as it is today and will be retired when OpenIddict supports CIMD.

## See Also

* [OpenIddict](index.md)
* [MCP Server](../aspnetcore-mcp/index.md)
* [MCP Authorization: Client Registration](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/client-registration)
* [OpenIddict Module](https://abp.io/docs/latest/modules/openiddict)
