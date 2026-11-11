```json
//[doc-seo]
{
    "Description": "Host Dignite Drive in an ABP application's own process, or once as its own service shared by several applications - and what the second needs: a shared authorization server, tenant resolution, Data Protection keys and a distributed cache."
}
```

# Hosting Modes

> Drive has not been released yet; see the [overview](index.md).

The modules that use the Drive reference only the provider package, so where the Drive runs is decided by the hosts alone. There are two ways, and **one Drive per deployment environment**: never two hosts each installing it behind one gateway.

## In the Application's Process

One host installs the Drive ([Setting Up the Drive](drive-host.md)) and the modules that use it. The provider calls the Drive directly, as a trusted caller: no allow-list, no tokens.

* Install `Dignite.Drive.Application`, `Dignite.Drive.HttpApi` (for public read, tickets and the file API) and the database provider; call `ConfigureDrive()` in the migrations `DbContext`.
* Leave `AbpBlobStoringDriveOptions.PublicBaseUrl` empty: public and ticket addresses are relative to the host.

This is the simplest mode, and the one to start with.

## The Drive as Its Own Service

One service installs the Drive; the applications install the provider and `Dignite.Drive.HttpApi.Client` ([The Remote Drive](provider.md#the-remote-drive)). The service can declare its containers in `appsettings.json` ([Declaring Containers in Configuration](drive-host.md#declaring-containers-in-configuration)), so it needs no code of its own beyond its host module.

What this mode needs:

* **One authorization server.** The applications get client-credentials tokens from it, and the Drive validates them (the Drive is an API resource of it - the `Drive` scope above). Each application is an OAuth client, listed in the `AllowedClientIds` of the containers it uses.
* **Tenants.** The proxies send the tenant with each call; the Drive must resolve it (ABP's header and query-string resolvers do, by default) and know the same tenants - in an ABP microservice solution, from the shared tenant management data. Behind a gateway, the gateway passes the tenant header on.
* **Routes.** The gateway routes `/api/drive/*` to the Drive - the BLOB API, the file API, public reads and tickets.
* **The public address.** Set `AbpBlobStoringDriveOptions.PublicBaseUrl` in the applications to the Drive's (or the gateway's) public address, so the addresses they build for browsers reach it.
* **Several instances.** A shared [Data Protection](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview) key ring, for [tickets](tickets.md#running-several-drive-instances), and a shared distributed cache and [distributed lock](https://abp.io/docs/latest/framework/infrastructure/distributed-locking) provider. The lock serves tickets and content dedup: an upload and the deletion of the same bytes take turns on it, and ABP's default lock only covers one process.

For the applications alone, it does **not** need permission data: the Drive authorizes applications with the allow-list in its own container configuration, so it needs neither the authorization server's tables nor shared permission grants.

The [file API](attaching-files.md) adds what any ABP service with users needs:

* **User tokens** from the same authorization server, for the file API.
* **Permission data.** The Drive's [permissions](permissions.md) are stored by ABP's Permission Management, in the tables the other services share, so the roles granted in the administration UI reach the Drive.
* **The UI's address.** An Angular application points `apis.Drive` at the Drive or the gateway ([Angular UI](angular-ui.md#installation)); an MCP server sets `DriveMcpOptions.BaseUrl` ([MCP Tools](mcp.md#options)).

## See Also

* [Setting Up the Drive](drive-host.md)
* [Using the Drive From an Application](provider.md)
