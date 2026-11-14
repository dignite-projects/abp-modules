```json
//[doc-seo]
{
    "Description": "Host Dignite Blob Management in an ABP application's own process, or once as its own service shared by several applications - and what the second needs: a shared authorization server, tenant resolution, routes, a Data Protection key ring, CORS, a CDN and a distributed lock."
}
```

# Hosting Modes

> Blob Management has not been released yet; see the [overview](index.md).

The modules that store through Blob Management reference only the provider package and, for files kept by id, `Dignite.Abp.BlobManagement.Application.Contracts`, so where it runs is decided by the hosts alone. There are two ways, and **one Blob Management per deployment environment**: never two hosts each installing it behind one gateway.

## In the Application's Process

One host installs Blob Management ([Setting Up](setting-up.md)) and the modules that use it. The provider and the [file service](files.md) call it directly, as a trusted caller: no grant, no tokens.

* Install `Dignite.Abp.BlobManagement.Application`, `Dignite.Abp.BlobManagement.HttpApi` (for public reads, the browser's uploads with a ticket and reads with a link, and the APIs the management page calls) and the database provider; call `ConfigureBlobManagement()` in the migrations `DbContext`.
* Leave `AbpBlobStoringBlobManagementOptions.PublicBaseUrl` empty: public addresses, upload addresses and read links are relative to the host.
* Upload tickets and read links are signed with the host's [Data Protection](#data-protection) key ring, which an ABP host already persists for its cookies when it runs more than one instance.

This is the simplest mode, and the one to start with.

## Blob Management as Its Own Service

One service installs Blob Management; the applications install the provider and `Dignite.Abp.BlobManagement.HttpApi.Client` ([A Remote Blob Management](provider.md#a-remote-blob-management)), whose proxies serve both the provider and the file service. The service can declare its containers in `appsettings.json` ([Declaring Containers in Configuration](setting-up.md#declaring-containers-in-configuration)), so it needs no code of its own beyond its host module.

A business system joins in three steps: the authorization server registers its OAuth client; Blob Management declares the containers it uses and is deployed; the host's administrator grants the client those containers' files ([Granting an Application](permissions.md#granting-an-application)).

What this mode needs:

* **One authorization server.** The applications get client-credentials tokens from it, and Blob Management validates them (the service is an API resource of it). Each application is an OAuth client, granted the files of the containers it uses. The token reaches every tenant - the proxies name the tenant of each call - so it never leaves the application's back end.
* **Tenants.** The proxies send the tenant with each call; the service must resolve it (ABP's header and query-string resolvers do, by default) and know the same tenants - in an ABP microservice solution, from the shared tenant management data. Behind a gateway, the gateway passes the tenant header on. The browser's uploads and reads name no tenant: their ticket or link does.
* **Routes.** The gateway routes two prefixes to the service: `/api/blob-management/*` - the BLOB and file APIs, the container, directory and usage APIs, the uploads with a ticket and the reads with a link - and **`/blobs/*`**, the public reads.
* **The public address.** Set `AbpBlobStoringBlobManagementOptions.PublicBaseUrl` in the applications to the service's (or the gateway's) public address, so the addresses they build for browsers reach it. A file's `url`, a ticket's `uploadUrl` and a link's `url` are relative to that address as well: the application puts it in front before it hands them to a browser.
* **Data Protection.** See [below](#data-protection).
* **CORS.** A browser uploads to the service from the business system's pages, a cross-origin `POST` with a body: the service's CORS policy allows those origins (`App:CorsOrigins` in an ABP host) on `/api/blob-management/uploads/*`. Reads with a link and public reads are plain `GET`s an `<img>` or a link makes, and need no CORS unless a script fetches them.
* **A CDN in front of `/blobs`.** Public reads are `Cache-Control: public` for a day, with an `ETag`. The cache key must include the query string: the tenant (`__tenant`) and the size (`Width`, `Height`) are there, and a key without them serves one tenant's file, or one size, for another. Reads with a link are private and not for a shared cache.
* **Several instances.** A shared distributed cache, as for any ABP service - read links are kept in it, so that one file gets one link per window whichever instance issued it - and a shared [distributed lock](https://abp.io/docs/latest/framework/infrastructure/distributed-locking) provider. The lock serves content dedup - an upload and the deletion of the same bytes take turns on it - saving under a name - two saves under one name take turns on it - and the cleanup of pending files, which one instance runs at a time. ABP's default lock only covers one process.
* **Permission data.** An application's grant on a container, like every other of Blob Management's [permissions](permissions.md), is stored by ABP's Permission Management, in the tables the other services share: the service reads it. To grant a client in the resource permission dialog - choose *Application*, find the client, tick `Files` - the host the dialog runs against installs `Volo.Abp.PermissionManagement.Domain.OpenIddict` next to the OpenIddict domain; without them, grant it with `SetForClientAsync` in a data seed ([Granting an Application](permissions.md#granting-an-application)).

The management page, the [usage](usage.md) and the [directories](directories.md) add what any ABP service with users needs:

* **User tokens** from the same authorization server.
* **User names**, if the usage by user should show them: ABP's `IExternalUserLookupServiceProvider`, such as the Identity module's HTTP client ([Usage](usage.md#the-api)).
* **The UI's address.** An Angular application points `apis.BlobManagement` at the service or the gateway ([Angular UI](angular-ui.md#installation)).

## Data Protection

Upload tickets and read links are signed and encrypted with ASP.NET Core [Data Protection](https://learn.microsoft.com/aspnet/core/security/data-protection/introduction), and only Blob Management reads them: a business system asks for one and hands it on, and never holds the keys.

* **Persist the key ring.** By default it is kept in memory or the user profile and changes with each deployment, which turns every ticket and link handed out into a 403.
* **Share it among Blob Management's instances only**, with one application name: a ticket one instance signs is read by whichever instance the browser reaches. Do not share it with the business systems: whoever holds the keys can write tickets.

````csharp
context.Services.AddDataProtection()
    .SetApplicationName("BlobManagement")
    .PersistKeysToStackExchangeRedis(redis, "BlobManagement-Protection-Keys");
````

When Blob Management is installed in the application's own process, it shares that host's key ring, which the host persists for its own cookies anyway.

## See Also

* [Setting Up](setting-up.md)
* [Using It From an Application](provider.md)
* [Keeping Files by Id](files.md)
* [Permissions](permissions.md)
