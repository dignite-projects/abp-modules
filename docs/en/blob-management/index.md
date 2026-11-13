```json
//[doc-seo]
{
    "Description": "Dignite Blob Management: an ABP infrastructure module that manages a tenant's BLOB containers - a BLOB storing provider for applications, a file API, directories, permissions by place, a back office, a file input and MCP tools."
}
```

# Blob Management

> **Blob Management is in development and has not been released yet.** Its packages are not published. These pages describe what is built.

Blob Management is an **ABP infrastructure module** that manages a tenant's [BLOB containers](https://abp.io/docs/latest/framework/infrastructure/blob-storing). It runs inside an application's own process, or once as a service that several applications share.

The host declares which of its BLOB containers are managed. Everything in a managed container has a record - its name, type, size, hash, tenant and who wrote it - so the module can de-duplicate the bytes, count a tenant's storage, serve files publicly, list them, and let people work with them under permissions.

## Two Ways In

A managed container is reached in two ways, and both end in the same table of files:

| | From an application | From people |
|---|---|---|
| What it is | A [BLOB Storing](https://abp.io/docs/latest/framework/infrastructure/blob-storing) provider. `container.UseBlobManagement(...)` makes one of the application's own BLOB containers keep its bytes in a managed container ([Using It From an Application](provider.md)). | The file API, the back office, the file input and the explorer a business system opens ([Attaching Files](attaching-files.md)). |
| Who is authenticated | The calling application (an OAuth client, or a short-lived [ticket](tickets.md)), never its users. | The user. |
| How a file is addressed | By the **name** the application saved it under, which the application reads it back by. | By its **id**. Its name is a display name and may repeat. |
| Whose rules apply | The application's own pipeline contributors and authorization, then the managed container's rules. | Blob Management's [permissions](permissions.md), then the container's rules. |

The back office lists both kinds of file. A file saved under a name is read, downloaded and deleted like any other, but it is not renamed or moved: its name is how its application finds it.

What every managed container adds to plain BLOB storing:

* a **storage quota** for each tenant, across every container ([Setting Up](setting-up.md#tenant-storage));
* **de-duplication**: the same bytes stored twice in a container are kept once;
* **upload rules** per container, as presets of the [BLOB Storing](../blob-storing/index.md) pipeline contributors ([Presets](setting-up.md#presets));
* **public read** with image sizing, for containers read by anyone ([Public Addresses](provider.md#public-addresses));
* **tickets** that let a browser send a file, or fetch one, directly ([Tickets](tickets.md));
* **directories**, in a container that allows them ([Directories](directories.md)).

## What It Is Not

Blob Management manages the BLOB containers that business systems store in. It is **not a cloud drive**, and has no sharing of a file with other users: a file belongs to its container, and people reach it through the permissions of the container and of its directories. For a document management application with sharing for end users, use ABP's [File Management](https://abp.io/docs/latest/modules/file-management) module; Blob Management does not compete with it.

## How a File Is Stored

Every file has a record, a *file descriptor*: the container, its name, the physical BLOB that holds the bytes, the type detected from the bytes, the size, a SHA-256 hash of what was uploaded, the directory it is in (for an uploaded file in a container with directories), the tenant, and who wrote it (a user, or the OAuth client of an application).

* The **physical BLOB** is named by Blob Management - 32 random hexadecimal digits and the extension of the detected type - and several descriptors share one when they hold the same bytes; it is deleted with the last of them.
* The type is **detected from the bytes**, and a name whose extension contradicts the content is refused, before anything is stored.
* The container's pipeline contributors - its size limit, allowed types, image processing - run when the bytes are stored.

## Packages

| Package | Description |
|---|---|
| `Dignite.Abp.BlobStoring.BlobManagement` | The BLOB storing provider, the public address builder and the ticket client. The only package a business module needs. |
| `Dignite.Abp.BlobManagement.Domain.Shared`, `.Domain` | The file and directory descriptors and their managers, managed containers and their presets, the tenant features, the directory import. |
| `Dignite.Abp.BlobManagement.Application.Contracts`, `.Application` | The services the provider and the HTTP API run, the container, file and directory services and their authorization, the permissions, startup checks, tickets. |
| `Dignite.Abp.BlobManagement.HttpApi`, `.HttpApi.Client` | The HTTP API, and the C# client proxies a host uses to reach a remote Blob Management. |
| `Dignite.Abp.BlobManagement.EntityFrameworkCore`, `.MongoDB` | The database providers. |
| `Dignite.Abp.BlobManagement.Mcp` | [MCP tools](mcp.md) over the file and directory services. |
| `Dignite.Abp.BlobManagement.Installer` | The entry point ABP Studio and ABP Suite install the module through. |
| `@dignite/ng.blob-management` | [Angular UI](angular-ui.md): the management page, the explorer and file picker, the file input. |

## Pages

* [Setting Up](setting-up.md): install the module in a host, declare its containers in code or in configuration, presets, the tenant quota.
* [Using It From an Application](provider.md): `UseBlobManagement`, Blob Management in the same process or remote, public addresses.
* [Tickets](tickets.md): a browser storing or reading a file directly, and what that skips.
* [Hosting Modes](hosting.md): one host, or Blob Management as its own service, and what the second needs.
* [Attaching Files](attaching-files.md): the file API, upload links for users, public reads.
* [Directories](directories.md): containers that allow directories, listing, sorting, renaming, moving and deleting.
* [Permissions](permissions.md): the module-wide permissions, grants on a container and on a directory, and who may do what.
* [Angular UI](angular-ui.md): the management page, the explorer, the file picker and the file input.
* [MCP Tools](mcp.md): the `blob_*` tools for AI assistants.
* [Importing a Directory Tree](importing-directories.md): filing existing files into directories by a path they carry.

## See Also

* [BLOB Storing](../blob-storing/index.md)
