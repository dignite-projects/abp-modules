```json
//[doc-seo]
{
    "Description": "A checklist for adding Client ID Metadata Documents to an existing ABP authorization server: the decisions to make, the code to change, and how to check it works before ChatGPT or Claude connects."
}
```

# Adding It to an Authorization Server

[Client ID Metadata Documents](client-id-metadata-documents.md) describes what the package does and its options. This page is the checklist for adding it to an authorization server that already runs: the decisions to make first, the code to change, and how to check the result locally before any real client connects. It was written while moving the first production server over to the package.

## Decide First

Each of these has to be answered by the server, not by the package.

**Which scopes.** `Scopes` has no default. Use the scopes of the client you pre-registered for people who add the connector by hand, if there is one: a user then gets the same consent and the same token from either route. Typically that is your API's scope plus `profile` and `email`.

**What happens to the `resource` parameter.** MCP clients send an [RFC 8707](https://datatracker.ietf.org/doc/html/rfc8707) `resource` parameter that names the MCP endpoint, and OpenIddict refuses it unless the server is set up for it. A server does one of two things:

* It **protects one resource**, and the token's audience comes from the scope. It turns both checks off with `DisableResourceValidation()` and `IgnoreResourcePermissions()`, and has nothing more to do.
* It **protects several resources**, such as one MCP endpoint per service behind a gateway. It registers each resource with `RegisterResources`, in both forms, with and without the trailing slash. The package then grants every CIMD client an `rsrc:` permission for each of them, so no data seeder has to.

Find out which one your server is before you start: the second without `RegisterResources` fails every request with `ID2190`, and the first is unaffected by anything this package does.

**Which client hosts.** In production, set `AllowedHosts` to the hosts of the clients you support, such as `claude.ai` and `chatgpt.com`. Leave it empty only if any client on the internet may register. See [Production](client-id-metadata-documents.md#production).

**Whether `private_key_jwt` may disappear from discovery.** The package removes it from `token_endpoint_auth_methods_supported` by default, for the whole server. Search your client data seeder for `private_key_jwt` or `ClientAuthenticationMethods.PrivateKeyJwt`; if a pre-registered confidential client uses it, set `RemovePrivateKeyJwtFromDiscovery` to `false`.

**The consent page language.** The default display name is English, `Claude (claude.ai)`. If your consent page is in another language, plan the override described in [Consent Page Display Name](client-id-metadata-documents.md#consent-page-display-name).

## Change the Code

1. **Add the package** to the project that hosts the OpenIddict server, not to the MCP server or the API project, if those are separate applications:

   ````xml
   <PackageReference Include="Dignite.Abp.OpenIddict.ClientIdMetadataDocuments" Version="10.7.6" />
   ````

   It is published to NuGet.org. If the solution's `NuGet.Config` uses `packageSourceMapping`, make sure a pattern maps `Dignite.*` to `nuget.org`, or the restore fails.

2. **Depend on the module** in the host module:

   ````csharp
   [DependsOn(typeof(AbpOpenIddictClientIdMetadataDocumentsModule))]
   ````

   That is all the wiring: the module adds its two OpenIddict event handlers, its HttpClient and its background worker. If the server has CIMD handlers of its own from before, remove them and their registration: both would run, and two handlers would fetch and register the same client.

3. **Configure the options**: scopes in code, then anything you want to change per environment from configuration. The configuration binding adds to `Scopes` and `AllowedHosts` rather than replacing them, so keep the code part to the values every environment shares:

   ````csharp
   private void ConfigureClientIdMetadataDocuments(IConfiguration configuration)
   {
       Configure<AbpClientIdMetadataDocumentOptions>(options =>
       {
           options.Scopes.Add("MyApp");
           options.Scopes.Add("profile");
           options.Scopes.Add("email");
       });
       Configure<AbpClientIdMetadataDocumentOptions>(configuration.GetSection("AuthServer:ClientIdMetadataDocuments"));
   }
   ````

   ````json
   "AuthServer": {
     "ClientIdMetadataDocuments": {
       "AllowedHosts": [ "claude.ai", "chatgpt.com" ]
     }
   }
   ````

4. **Set up resources** as decided above. A server that protects several resources registers both forms of each:

   ````csharp
   PreConfigure<OpenIddictServerBuilder>(builder =>
   {
       builder.RegisterResources(new Uri(mcpUrl), new Uri(mcpUrl + "/"));
   });
   ````

5. **Override the display name**, if you decided to, by replacing `ClientIdMetadataDocumentClientProvisioner` with a subclass marked `[Dependency(ReplaceServices = true)]` and `[ExposeServices(typeof(ClientIdMetadataDocumentClientProvisioner))]`. Keep every redirect host in the name: a name without one of them is refused, and the client cannot sign in.

6. **Keep the pre-registered client.** Do not delete the client you seeded for manually added connectors. CIMD only handles `client_id` values that are `https` URLs, so the two coexist.

## Check the Deployment

**Outbound access.** The server fetches each client's document itself, from the internet, without a proxy. A server whose only route out is an HTTP proxy cannot fetch documents; give it direct outbound HTTPS.

**Background workers.** The cleanup worker runs only where ABP's background workers are enabled (`AbpBackgroundWorkerOptions.IsEnabled`). With several instances, keep them on in at least one, and use a real distributed lock provider (such as Redis) so that only one instance cleans at a time.

**Multi-tenancy.** Nothing to do: registrations always go to the host, whichever tenant the request resolved.

## Verify Locally

Run the server and check four things. The `client_id` below is Claude Code's real metadata document, which is served publicly and has `http://localhost` redirect URIs, so the server fetches a real document and nothing leaves your machine except that fetch.

1. **Discovery.** `GET /.well-known/openid-configuration` must contain `"client_id_metadata_document_supported": true`, and `token_endpoint_auth_methods_supported` must list `none` and, unless you turned the option off, not `private_key_jwt`.

2. **A real client gets to the sign-in page.**

   ````
   GET /connect/authorize?response_type=code
       &client_id=https%3A%2F%2Fclaude.ai%2Foauth%2Fclaude-code-client-metadata
       &redirect_uri=http%3A%2F%2Flocalhost%3A53682%2Fcallback
       &scope=openid%20MyApp
       &code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256
       &state=x
   ````

   The answer is a `302` to the login page. A server that protects several resources adds `&resource=` with its MCP URL, in both forms, one request each; an `ID2190` means the resource is not registered, an `ID2192` that the permission is missing.

3. **A bad client is refused.** The same request with a `client_id` whose URL serves no document answers `401` with `invalid_client`, without redirecting.

4. **The registration.** The application table has a row whose `ClientId` is the URL, whose `DisplayName` shows the redirect hosts (`Claude Code (localhost, 127.0.0.1)`), and whose `Properties` holds `cimd_expires_at`.

The full sign-in with Claude or ChatGPT needs the server reachable from the internet, since their redirect URIs are their own sites; do it on a staging server once the four checks pass.

## See Also

* [Client ID Metadata Documents](client-id-metadata-documents.md)
* [OpenIddict](index.md)
