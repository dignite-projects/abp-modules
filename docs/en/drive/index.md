```json
//[doc-seo]
{
    "Description": "Dignite Drive: a per-tenant cloud drive for ABP applications - a BLOB storing provider that keeps an application's bytes in the Drive, with tenant quotas, content de-duplication, public read with image sizing and upload tickets."
}
```

# Drive

> **Drive is in development and has not been released yet.** Its packages are not published. These pages describe what is built so far: the storage the applications use (the BLOB storing provider and everything behind it). The file library for people - directories, the file picker, the Drive's own permissions - comes next. The plan is in [issue #379](https://github.com/dignite-projects/abp-modules/issues/379).

Drive is a **per-tenant cloud drive** for ABP applications. It runs inside an application's own process, or once as a service that several applications share.

## Two Ways In

| | From an application | From people |
|---|---|---|
| What it is | A [BLOB Storing](https://abp.io/docs/latest/framework/infrastructure/blob-storing) provider. `container.UseDrive(...)` makes any of the application's own blob containers keep its bytes in the Drive. | A file library: directories, a file picker, and the Drive's own permissions, down to a single file. *Not built yet.* |
| Who is authenticated | The calling application (an OAuth client, or a short-lived ticket), never its users. | The user. |
| Whose rules apply | The application's own: its pipeline contributors and its authorization run before the bytes reach the Drive. Then the Drive container's rules. | The Drive's own permissions. |
| Containers | **System** containers, declared on the Drive and named by applications. Not shown in any UI. | **User drives**. |

An application keeps its own file metadata and its own authorization, and hands only bytes to the Drive. The Drive adds what an application would otherwise build itself:

* a **storage quota** for each tenant, across every application that stores in the Drive ([Setting Up the Drive](drive-host.md#tenant-storage));
* **de-duplication**: the same bytes stored twice in a container are kept once;
* **upload rules** per Drive container, as presets of the [BLOB Storing](../blob-storing/index.md) pipeline contributors;
* **public read** with image sizing, for containers read by anyone ([Using the Drive From an Application](provider.md#public-addresses));
* **tickets** that let a browser send a file to the Drive, or fetch one, directly ([Tickets](tickets.md)).

## How a File Is Stored

Every file the Drive holds has a record, a *file descriptor*: the Drive container, the name the application gave it, the physical BLOB that holds the bytes, the type detected from the bytes, the size, a SHA-256 hash of what was uploaded, the tenant, and who wrote it (a user, or the OAuth client of an application).

* The application addresses a file by **its own name**, as with any other BLOB provider. The physical BLOB is named by the Drive, and several descriptors share one when they hold the same bytes; it is deleted with the last of them.
* The type is **detected from the bytes**, and a name whose extension contradicts the content is refused, before anything is stored.
* The Drive container's pipeline contributors - its size limit, allowed types, image processing - run when the bytes are stored.

## Packages

| Package | Description |
|---|---|
| `Dignite.Abp.BlobStoring.Drive` | The BLOB storing provider, the public address builder and the ticket client. The only package a business module needs. |
| `Dignite.Drive.Domain.Shared`, `.Domain` | The file descriptor and its manager, Drive containers and their presets, the tenant features. |
| `Dignite.Drive.Application.Contracts`, `.Application` | The services the provider and the HTTP API run, startup checks, tickets. |
| `Dignite.Drive.HttpApi`, `.HttpApi.Client` | The HTTP API, and the C# client proxies a host uses to reach a remote Drive. |
| `Dignite.Drive.EntityFrameworkCore`, `.MongoDB` | The database providers. |
| `Dignite.Drive.Mcp` | MCP tools over the file library. *Not built yet.* |
| `@dignite/ng.drive` | Angular UI: the file picker and the file browser. *Not built yet.* |

## Pages

* [Setting Up the Drive](drive-host.md): install it in a host, declare its containers in code or in configuration, the tenant quota.
* [Using the Drive From an Application](provider.md): `UseDrive`, the Drive in the same process or remote, public addresses.
* [Tickets](tickets.md): a browser storing or reading a file directly, and what that skips.
* [Hosting Modes](hosting.md): one host, or the Drive as its own service, and what the second needs.

## See Also

* [BLOB Storing](../blob-storing/index.md)
