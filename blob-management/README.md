# Dignite Blob Management

BLOB management for ABP applications: a BLOB storing provider with a ledger. Applications store their bytes through it;
it keeps a record of every BLOB, and from those records counts what the tenant, each container, each user and each
client store, against the tenant's allocation. An infrastructure module, hostable in the application's own process or
as one service.

> **Status: in development, not yet published.** Built: the provider and the file service applications store through,
> the managed containers and their presets, the tenant storage allocation and the usage figures, content
> de-duplication, the HTTP API, public read with image sizing, directories where a container allows them, an Angular
> management page and read-only MCP tools.

An application keeps its own rules and calls Blob Management for its users:
`container.UseBlobManagement(provider => provider.BlobManagementContainer = "media")` makes any of its BLOB containers
store its bytes in Blob Management, after the application's own pipeline contributors have run, and
`IBlobManagementFileService` keeps the files of a business system's file library by id. In process the application is
trusted; remotely it calls as an OAuth client the container allows, never with a user's token.

Blob Management has no upload of its own and is not shown to ordinary users. Administrators read the usage, the
containers, their directories and files on the management page of `@dignite/ng.blob-management`; users granted the
permission keep a container's directories.

It is not a cloud drive: there are no personal drives and no sharing of a file with someone. ABP's File Management does
those.

> **.NET 10 · ABP 10.7 · LGPL-3.0-only**

## Documentation

**[Read the documentation](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/index.md)**:

* [Setting Up Blob Management](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/setting-up.md) -
  installing it in a host, declaring its containers in code or in configuration, the tenant allocation.
* [Using Blob Management From an Application](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/provider.md) -
  `UseBlobManagement`, Blob Management in the same process or remote, public addresses.
* [Keeping Files by Id](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/files.md) -
  `IBlobManagementFileService`, what a business system's file library calls, and who a file is counted for.
* [Hosting Modes](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/hosting.md) - one
  host, or Blob Management as its own service.
* [Directories](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/directories.md) -
  containers with directories, the tree and its API.
* [Usage](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/usage.md) - stored and
  uploaded bytes, by tenant, container, user and client.
* [Permissions](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/permissions.md) - the
  management permission and the directory permissions, module-wide and on a container.
* [Angular UI](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/angular-ui.md) - the
  management page.
* [MCP Tools](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/mcp.md) - the read-only
  `blob_*` tools for AI assistants.
* [Importing Directories](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/importing-directories.md) -
  bringing an application's own file library in.
