```json
//[doc-seo]
{
    "Description": "Host Dignite Blob Management in an ABP application's own process, or once as its own service shared by several applications - and what the second needs: a shared authorization server, tenant resolution, Data Protection keys and a distributed cache."
}
```

# Hosting Modes

> Blob Management has not been released yet; see the [overview](index.md).

The modules that store through Blob Management reference only the provider package, so where it runs is decided by the hosts alone. There are two ways, and **one Blob Management per deployment environment**: never two hosts each installing it behind one gateway.

## In the Application's Process

One host installs Blob Management ([Setting Up](setting-up.md)) and the modules that use it. The provider calls it directly, as a trusted caller: no allow-list, no tokens.

* Install `Dignite.Abp.BlobManagement.Application`, `Dignite.Abp.BlobManagement.HttpApi` (for public read, tickets and the file API) and the database provider; call `ConfigureBlobManagement()` in the migrations `DbContext`.
* Leave `AbpBlobStoringBlobManagementOptions.PublicBaseUrl` empty: public and ticket addresses are relative to the host.

This is the simplest mode, and the one to start with.

## Blob Management as Its Own Service

One service installs Blob Management; the applications install the provider and `Dignite.Abp.BlobManagement.HttpApi.Client` ([A Remote Blob Management](provider.md#a-remote-blob-management)). The service can declare its containers in `appsettings.json` ([Declaring Containers in Configuration](setting-up.md#declaring-containers-in-configuration)), so it needs no code of its own beyond its host module.

What this mode needs:

* **One authorization server.** The applications get client-credentials tokens from it, and Blob Management validates them (the service is an API resource of it). Each application is an OAuth client, listed in the `AllowedClientIds` of the containers it uses.
* **Tenants.** The proxies send the tenant with each call; the service must resolve it (ABP's header and query-string resolvers do, by default) and know the same tenants - in an ABP microservice solution, from the shared tenant management data. Behind a gateway, the gateway passes the tenant header on.
* **Routes.** The gateway routes `/api/blob-management/*` to the service - the BLOB API, the file, directory and container APIs, public reads and tickets.
* **The public address.** Set `AbpBlobStoringBlobManagementOptions.PublicBaseUrl` in the applications to the service's (or the gateway's) public address, so the addresses they build for browsers reach it.
* **Several instances.** A shared [Data Protection](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview) key ring, for [tickets](tickets.md#running-several-instances), and a shared distributed cache and [distributed lock](https://abp.io/docs/latest/framework/infrastructure/distributed-locking) provider. The lock serves tickets and content dedup: an upload and the deletion of the same bytes take turns on it, and ABP's default lock only covers one process.

For the applications alone, it does **not** need permission data: Blob Management authorizes applications with the allow-list in its own container configuration, so it needs neither the authorization server's tables nor shared permission grants.

The [file API](attaching-files.md), the [directories](directories.md) and the back office add what any ABP service with users needs:

* **User tokens** from the same authorization server.
* **Permission data.** Blob Management's [permissions](permissions.md), the grants on containers and directories included, are stored by ABP's Permission Management, in the tables the other services share, so the roles granted in the administration UI reach the service.
* **The UI's address.** An Angular application points `apis.BlobManagement` at the service or the gateway ([Angular UI](angular-ui.md#installation)); an MCP server sets `BlobManagementMcpOptions.BaseUrl` ([MCP Tools](mcp.md#options)).

## See Also

* [Setting Up](setting-up.md)
* [Using It From an Application](provider.md)
