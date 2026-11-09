```json
//[doc-seo]
{
    "Description": "Additions to the ABP Framework OpenIddict module for an authorization server, starting with OAuth Client ID Metadata Documents for MCP clients such as ChatGPT and Claude."
}
```

# OpenIddict

The `openiddict` packages extend ABP's [OpenIddict module](https://abp.io/docs/latest/modules/openiddict) on an **authorization server**. They hold no domain model of their own and replace none of OpenIddict's services: each one plugs in through OpenIddict's event handlers, and stores what it needs in OpenIddict's own tables.

They are independent of the other modules in this repository. In particular, the authorization server an MCP client signs in to is often not the application that hosts the [MCP server](../aspnetcore-mcp/index.md), so neither package depends on the other.

## Packages

| Package | Description |
|---|---|
| `Dignite.Abp.OpenIddict.ClientIdMetadataDocuments` | Accepts OAuth Client ID Metadata Documents: a client whose `client_id` is an HTTPS URL is registered from the JSON document at that URL, with no pre-registration and no dynamic client registration. The registration ChatGPT's and Claude's MCP connectors use. See [Client ID Metadata Documents](client-id-metadata-documents.md). |

## See Also

* [Client ID Metadata Documents](client-id-metadata-documents.md)
* [OpenIddict Module](https://abp.io/docs/latest/modules/openiddict)
