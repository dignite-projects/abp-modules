```json
//[doc-seo]
{
    "Description": "Host Dignite Blob Management in an ABP application's own process, or once as its own service shared by several applications - and what the second needs: a shared authorization server, tenant resolution, routes and a distributed lock."
}
```

# Hosting Modes

> Blob Management has not been released yet; see the [overview](index.md).

The modules that store through Blob Management reference only the provider package and, for files kept by id, `Dignite.Abp.BlobManagement.Application.Contracts`, so where it runs is decided by the hosts alone. There are two ways, and **one Blob Management per deployment environment**: never two hosts each installing it behind one gateway.

## In the Application's Process

One host installs Blob Management ([Setting Up](setting-up.md)) and the modules that use it. The provider and the [file service](files.md) call it directly, as a trusted caller: no grant, no tokens.

* Install `Dignite.Abp.BlobManagement.Application`, `Dignite.Abp.BlobManagement.HttpApi` (for public read and the APIs the management page calls) and the database provider; call `ConfigureBlobManagement()` in the migrations `DbContext`.
* Leave `AbpBlobStoringBlobManagementOptions.PublicBaseUrl` empty: public addresses are relative to the host.

This is the simplest mode, and the one to start with.

## Blob Management as Its Own Service

One service installs Blob Management; the applications install the provider and `Dignite.Abp.BlobManagement.HttpApi.Client` ([A Remote Blob Management](provider.md#a-remote-blob-management)), whose proxies serve both the provider and the file service. The service can declare its containers in `appsettings.json` ([Declaring Containers in Configuration](setting-up.md#declaring-containers-in-configuration)), so it needs no code of its own beyond its host module.

What this mode needs:

* **One authorization server.** The applications get client-credentials tokens from it, and Blob Management validates them (the service is an API resource of it). Each application is an OAuth client, granted the files of the containers it uses ([Granting an Application](permissions.md#granting-an-application)).
* **Tenants.** The proxies send the tenant with each call; the service must resolve it (ABP's header and query-string resolvers do, by default) and know the same tenants - in an ABP microservice solution, from the shared tenant management data. Behind a gateway, the gateway passes the tenant header on.
* **Routes.** The gateway routes `/api/blob-management/*` to the service - the BLOB and file APIs, the container, directory and usage APIs, and public reads.
* **The public address.** Set `AbpBlobStoringBlobManagementOptions.PublicBaseUrl` in the applications to the service's (or the gateway's) public address, so the addresses they build for browsers reach it.
* **Bytes that are not public pass through the application.** A browser does not reach the service for them: the application receives an upload and stores it, and reads a file and hands it on.
* **Several instances.** A shared distributed cache, as for any ABP service, and a shared [distributed lock](https://abp.io/docs/latest/framework/infrastructure/distributed-locking) provider. The lock serves content dedup - an upload and the deletion of the same bytes take turns on it - and saving under a name: two saves under one name take turns on it. ABP's default lock only covers one process.
* **Permission data.** An application's grant on a container, like every other of Blob Management's [permissions](permissions.md), is stored by ABP's Permission Management, in the tables the other services share: the service reads it, and it is granted where the clients are known ([Granting an Application](permissions.md#granting-an-application)).

The management page, the [usage](usage.md) and the [directories](directories.md) add what any ABP service with users needs:

* **User tokens** from the same authorization server.
* **User names**, if the usage by user should show them: ABP's `IExternalUserLookupServiceProvider`, such as the Identity module's HTTP client ([Usage](usage.md#the-api)).
* **The UI's address.** An Angular application points `apis.BlobManagement` at the service or the gateway ([Angular UI](angular-ui.md#installation)).

## See Also

* [Setting Up](setting-up.md)
* [Using It From an Application](provider.md)
* [Keeping Files by Id](files.md)
