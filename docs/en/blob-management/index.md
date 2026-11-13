```json
//[doc-seo]
{
    "Description": "Dignite Blob Management: an ABP infrastructure module - a BLOB storing provider with a ledger, which keeps a record of every BLOB applications store and from it counts a tenant's storage, by container, user and client, for a read-only management page."
}
```

# Blob Management

> **Blob Management is in development and has not been released yet.** Its packages are not published. These pages describe what is built.

Blob Management is an **ABP infrastructure module**: a [BLOB storing](https://abp.io/docs/latest/framework/infrastructure/blob-storing) provider with a ledger. Business systems store their bytes through it; it writes a record of each BLOB, and from those records come the tenant's storage allocation, the usage figures and a management page for administrators. It runs inside an application's own process, or once as a service that several applications share.

It serves business systems. It has no upload of its own and is not shown to ordinary users: a business system builds the UI its users store, rename, move and delete files with, applies its own rules, and calls Blob Management for them.

## Who Calls It

| Caller | Admitted by | Does |
|---|---|---|
| An application | In the same process: trusted. Over HTTP: an OAuth client the container allows (`AllowedClientIds`), with a token that carries no user. | Stores and reads BLOBs by name ([Using It From an Application](provider.md)) and files by id ([Keeping Files by Id](files.md)). |
| Anyone | The address of a file in a `PublicRead` container. | Reads it, resized if it is a picture ([Public Addresses](provider.md#public-addresses)). |
| A user | A directory permission, module-wide or granted on the container. | Creates, renames, moves and deletes directories ([Directories](directories.md)). |
| An administrator | `BlobManagement.Containers`. | Reads the usage, the containers, their directories and files, and the files' bytes ([Usage](usage.md), [Angular UI](angular-ui.md)). |

No API lets an end user store, change or remove a file, and none lets a user read one without the management permission ([Permissions](permissions.md)).

## Two Ways to Address a File

An application reaches a managed container in two ways, and both end in the same table of files:

| | BLOBs by name | Files by id |
|---|---|---|
| What it is | ABP's `IBlobContainer` with `container.UseBlobManagement(...)`, or `IBlobManagementBlobService`. | `IBlobManagementFileService`: list a place, read one file and its bytes, store, rename, move, delete. |
| How a file is addressed | By the **name** the application saved it under, unique among the container's files. | By its **id**. Its name is a display name and may repeat. |
| Where it sits | At the top of the container. It is never renamed or moved. | At the top of the container, or in a [directory](directories.md). |
| What it suits | An application's own BLOB containers, unchanged. | A business system's file library: attachments, documents, media. |

What every managed container adds to plain BLOB storing:

* a **storage allocation** for each tenant, across every container ([Setting Up](setting-up.md#tenant-storage));
* **usage figures**: what the tenant, each container, each user and each client store ([Usage](usage.md));
* **de-duplication**: the same bytes stored twice in a container are kept once;
* **upload rules** per container, as presets of the [BLOB Storing](../blob-storing/index.md) pipeline contributors ([Presets](setting-up.md#presets));
* **public read** with image sizing, for containers read by anyone ([Public Addresses](provider.md#public-addresses));
* **directories**, in a container that allows them ([Directories](directories.md)).

## What It Is Not

Blob Management keeps and counts the BLOBs business systems store. It is **not a cloud drive**: no personal drives, no sharing of a file with other users, no file manager for end users. For a document management application with sharing for end users, use ABP's [File Management](https://abp.io/docs/latest/modules/file-management) module; Blob Management does not compete with it.

## How a File Is Stored

Every file has a record, a *file descriptor*: the container, its name, the physical BLOB that holds the bytes, the type detected from the bytes, the size, a SHA-256 hash of what was uploaded, the directory it is in (for a file addressed by id in a container with directories), the tenant, the user it is counted for (`CreatorId`) and the OAuth client that stored it over HTTP (`ClientId`).

* The **physical BLOB** is named by Blob Management - 32 random hexadecimal digits and the extension of the detected type - and several descriptors share one when they hold the same bytes; it is deleted with the last of them.
* The type is **detected from the bytes**, and a name whose extension contradicts the content is refused, before anything is stored.
* The container's pipeline contributors - its size limit, allowed types, image processing - run when the bytes are stored.

## Packages

| Package | Description |
|---|---|
| `Dignite.Abp.BlobStoring.BlobManagement` | The BLOB storing provider and the public address builder. The only package a business module that stores by name needs. |
| `Dignite.Abp.BlobManagement.Domain.Shared`, `.Domain` | The file and directory descriptors and their managers, managed containers and their presets, the tenant features, the directory import. |
| `Dignite.Abp.BlobManagement.Application.Contracts`, `.Application` | `IBlobManagementBlobService` and `IBlobManagementFileService` with their in-process implementations, the container, directory and usage services and their authorization, the permissions, startup checks, public reads. |
| `Dignite.Abp.BlobManagement.HttpApi`, `.HttpApi.Client` | The HTTP API, and the C# client proxies a host uses to reach a remote Blob Management. |
| `Dignite.Abp.BlobManagement.EntityFrameworkCore`, `.MongoDB` | The database providers. |
| `Dignite.Abp.BlobManagement.Mcp` | Read-only [MCP tools](mcp.md) over the containers, directories and files. |
| `Dignite.Abp.BlobManagement.Installer` | The entry point ABP Studio and ABP Suite install the module through. |
| `@dignite/ng.blob-management` | [Angular UI](angular-ui.md): the management page - usage, directories, files read-only. |

## Pages

* [Setting Up](setting-up.md): install the module in a host, declare its containers in code or in configuration, presets, the tenant allocation.
* [Using It From an Application](provider.md): `UseBlobManagement`, Blob Management in the same process or remote, public addresses.
* [Keeping Files by Id](files.md): `IBlobManagementFileService`, what a business system's file library calls, and who a file is counted for.
* [Hosting Modes](hosting.md): one host, or Blob Management as its own service, and what the second needs.
* [Directories](directories.md): containers that allow directories, the tree, and the directory API.
* [Usage](usage.md): stored and uploaded bytes, by tenant, container, user and client.
* [Permissions](permissions.md): the module-wide permissions, grants on a container, and who may do what.
* [Angular UI](angular-ui.md): the management page and its parts.
* [MCP Tools](mcp.md): the read-only `blob_*` tools for AI assistants.
* [Importing a Directory Tree](importing-directories.md): filing existing files into directories by a path they carry.

## See Also

* [BLOB Storing](../blob-storing/index.md)
